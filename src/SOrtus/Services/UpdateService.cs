using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using SOrtus.Updates;

namespace SOrtus.Services;

/// <summary>更新检查结果。</summary>
internal enum UpdateCheckStatus
{
    /// <summary>当前已是最新版本。</summary>
    UpToDate,

    /// <summary>发现新版本（尚未被用户跳过）。</summary>
    Available,

    /// <summary>发现新版本但用户已选择跳过该版本。</summary>
    Skipped,

    /// <summary>处于 24 小时节流窗口内，本次未发起网络请求。</summary>
    Throttled,

    /// <summary>检查失败（无网络 / 超时 / 限流 / 解析失败）。</summary>
    Failed,
}

/// <param name="Status">检查结果状态。</param>
/// <param name="Info">发现的新版本（仅 Available / Skipped 时非空）。</param>
/// <param name="Message">失败原因（仅 Failed 时非空，可直接写日志）。</param>
internal sealed record UpdateCheckResult(UpdateCheckStatus Status, UpdateInfo? Info, string? Message);

/// <summary>
/// 自动更新服务：检查 GitHub Release、下载安装包、校验完整性、备份数据、拉起安装器。
/// 设计约束（全部来自需求确认）：
/// 1) 永不阻塞启动——调用方负责 fire-and-forget，本类所有网络操作都带 5 秒超时；
/// 2) 下载先落 .part 再改名，配合 SHA256 校验，杜绝半截包被安装；
/// 3) 安装前必须备份用户数据（sortus.db / settings.json）；
/// 4) 任何失败都只写日志，不弹窗、不重试轰炸。
/// </summary>
internal sealed class UpdateService
{
    /// <summary>仓库所有者。与 git remote 一致，改动需同步发布脚本。</summary>
    private const string RepositoryOwner = "Song-1118";

    /// <summary>仓库名。</summary>
    private const string RepositoryName = "SOrtus";

    /// <summary>网络操作超时。超过这个时间视为不可用，静默放弃。</summary>
    private static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(5);

    /// <summary>自动检查的最小间隔。</summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    private static readonly HttpClient Http = CreateHttpClient();

    private static readonly string AppDataRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SOrtus");

    private static readonly string UpdatesDirectory = Path.Combine(AppDataRoot, "Updates");

    private static readonly string BackupsDirectory = Path.Combine(AppDataRoot, "backups");

    private static readonly string LogPath = Path.Combine(AppDataRoot, "logs", "update.log");

    private static readonly object LogLock = new();

    public static UpdateService Instance { get; } = new();

    /// <summary>当前运行版本（取自程序集，与 version.props 单一来源一致）。</summary>
    public SemanticVersion CurrentVersion { get; } = ReadCurrentVersion();

    /// <summary>安装目录（发布产物所在目录）。用于判断是否为"绿色版"外置运行。</summary>
    public static string InstallDirectory => AppContext.BaseDirectory;

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = NetworkTimeout,
        };

        // GitHub API 要求非空 User-Agent，否则直接 403
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SOrtus-Updater/1.0");
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private static SemanticVersion ReadCurrentVersion()
    {
        try
        {
            var assembly = Assembly.GetEntryAssembly() ?? typeof(UpdateService).Assembly;
            var version = assembly.GetName().Version;
            if (version is null)
            {
                return new SemanticVersion(0, 0, 0);
            }

            return new SemanticVersion(version.Major, version.Minor, version.Build < 0 ? 0 : version.Build);
        }
        catch
        {
            return new SemanticVersion(0, 0, 0);
        }
    }

    /// <summary>
    /// 检查更新。
    /// </summary>
    /// <param name="force">true 表示用户手动触发，跳过 24 小时节流。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<UpdateCheckResult> CheckAsync(bool force, CancellationToken cancellationToken = default)
    {
        var state = UpdateState.Load();

        if (!force && state.LastCheckUtc is { } last && DateTime.UtcNow - last < CheckInterval)
        {
            return new UpdateCheckResult(UpdateCheckStatus.Throttled, null, null);
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(NetworkTimeout);

        var url = $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        // ETag 命中可省下 47MB 之外的响应体解析，也降低触发限流的压力
        if (!string.IsNullOrEmpty(state.LastEtag)
            && request.Headers.TryAddWithoutValidation("If-None-Match", state.LastEtag))
        {
            // 已附加
        }

        try
        {
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutSource.Token)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                state.LastCheckUtc = DateTime.UtcNow;
                state.Save();
                return new UpdateCheckResult(UpdateCheckStatus.UpToDate, null, null);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var reason = response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining)
                    && remaining.FirstOrDefault() == "0"
                        ? "GitHub API 限流（未认证请求每小时 60 次/IP）"
                        : $"GitHub API 拒绝请求：{(int)response.StatusCode}";
                Log($"检查更新失败：{reason}");
                return new UpdateCheckResult(UpdateCheckStatus.Failed, null, reason);
            }

            if (!response.IsSuccessStatusCode)
            {
                var reason = $"GitHub API 返回 {(int)response.StatusCode}";
                Log($"检查更新失败：{reason}");
                return new UpdateCheckResult(UpdateCheckStatus.Failed, null, reason);
            }

            var etag = response.Headers.ETag?.ToString();
            var json = await response.Content.ReadAsStringAsync(timeoutSource.Token).ConfigureAwait(false);

            if (!GitHubReleaseParser.TryParse(json, out var info, out var parseError) || info is null)
            {
                Log($"解析 Release 失败：{parseError}");
                return new UpdateCheckResult(UpdateCheckStatus.Failed, null, parseError);
            }

            state.LastCheckUtc = DateTime.UtcNow;
            state.LastEtag = etag;
            state.Save();

            if (info.Version <= CurrentVersion)
            {
                return new UpdateCheckResult(UpdateCheckStatus.UpToDate, null, null);
            }

            if (!string.IsNullOrEmpty(state.SkippedVersion)
                && SemanticVersion.TryParse(state.SkippedVersion, out var skipped)
                && skipped >= info.Version)
            {
                Log($"发现 {info.Version}，但用户已跳过该版本");
                return new UpdateCheckResult(UpdateCheckStatus.Skipped, info, null);
            }

            Log($"发现新版本 {info.Version}（当前 {CurrentVersion}）");
            return new UpdateCheckResult(UpdateCheckStatus.Available, info, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Log("检查更新超时（5 秒），已放弃");
            return new UpdateCheckResult(UpdateCheckStatus.Failed, null, "网络超时");
        }
        catch (HttpRequestException ex)
        {
            Log($"检查更新网络错误：{ex.Message}");
            return new UpdateCheckResult(UpdateCheckStatus.Failed, null, ex.Message);
        }
        catch (Exception ex)
        {
            Log($"检查更新异常：{ex}");
            return new UpdateCheckResult(UpdateCheckStatus.Failed, null, ex.Message);
        }
    }

    /// <summary>记录用户选择跳过某个版本。</summary>
    public void SkipVersion(SemanticVersion version)
    {
        var state = UpdateState.Load();
        state.SkippedVersion = version.ToString();
        state.Save();
    }

    /// <summary>
    /// 下载安装包到本地（先 .part 后改名），返回最终路径。
    /// 调用方必须先确保发布端提供了 SHA256，否则不应调用本方法。
    /// </summary>
    public async Task<string> DownloadAsync(
        UpdateInfo info,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(info.Sha256))
        {
            throw new InvalidOperationException("发布端未提供 SHA256 校验值，拒绝下载安装包。");
        }

        Directory.CreateDirectory(UpdatesDirectory);

        // 清理上一轮残留：.part 文件与旧版本安装包（保留正在下载的目标）
        CleanupDownloads(keepVersion: info.Version);

        var finalPath = Path.Combine(UpdatesDirectory, $"{GitHubReleaseParser.AssetNamePrefix}{info.Version}.exe");
        var partPath = finalPath + ".part";

        if (File.Exists(finalPath))
        {
            // 已下载过且校验通过则直接复用
            if (VerifySha256(finalPath, info.Sha256))
            {
                progress?.Report(1.0);
                Log($"复用已下载的安装包：{finalPath}");
                return finalPath;
            }

            File.Delete(finalPath);
        }

        EnsureDiskSpace(info.SizeBytes);

        Log($"开始下载 {info.Version}：{info.DownloadUri}");

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromMinutes(30));

        using var response = await Http.GetAsync(
                info.DownloadUri,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? info.SizeBytes;

        await using (var source = await response.Content.ReadAsStreamAsync(timeoutSource.Token).ConfigureAwait(false))
        await using (var target = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            var buffer = new byte[81920];
            long received = 0;
            int read;

            while ((read = await source.ReadAsync(buffer, timeoutSource.Token).ConfigureAwait(false)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), timeoutSource.Token).ConfigureAwait(false);
                received += read;

                if (totalBytes > 0)
                {
                    progress?.Report(Math.Clamp((double)received / totalBytes, 0, 1));
                }
            }
        }

        if (!VerifySha256(partPath, info.Sha256))
        {
            TryDelete(partPath);
            Log("下载完成但 SHA256 校验失败，已删除损坏文件");
            throw new InvalidDataException("安装包校验失败（SHA256 不匹配），可能下载不完整或已被篡改。");
        }

        // 校验通过才改名，确保磁盘上出现的 .exe 一定是完整且正确的
        File.Move(partPath, finalPath, overwrite: true);
        progress?.Report(1.0);
        Log($"下载完成并通过校验：{finalPath}");

        return finalPath;
    }

    /// <summary>计算并比对 SHA256（大小写不敏感）。</summary>
    public static bool VerifySha256(string filePath, string expectedHex)
    {
        if (string.IsNullOrWhiteSpace(expectedHex) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            var hash = SHA256.HashData(stream);
            var actual = Convert.ToHexString(hash).ToLowerInvariant();
            return string.Equals(actual, expectedHex.Trim().ToLowerInvariant(), StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            Log($"校验 SHA256 失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 安装前备份用户数据（数据库 + 设置），返回备份目录；无数据可备份时返回 null。
    /// 只保留最近 5 份，避免长期使用后占满磁盘。
    /// </summary>
    public string? BackupUserData()
    {
        try
        {
            var dbPath = Path.Combine(AppDataRoot, "sortus.db");
            var settingsPath = Path.Combine(AppDataRoot, "settings.json");

            if (!File.Exists(dbPath) && !File.Exists(settingsPath))
            {
                return null;
            }

            var target = Path.Combine(BackupsDirectory, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(target);

            if (File.Exists(dbPath))
            {
                File.Copy(dbPath, Path.Combine(target, "sortus.db"), overwrite: true);
            }

            if (File.Exists(settingsPath))
            {
                File.Copy(settingsPath, Path.Combine(target, "settings.json"), overwrite: true);
            }

            PruneBackups(keep: 5);
            Log($"已备份用户数据到 {target}");
            return target;
        }
        catch (Exception ex)
        {
            // 备份失败不阻断更新，但必须留痕：用户回滚时需要知道没有备份
            Log($"备份用户数据失败：{ex}");
            return null;
        }
    }

    /// <summary>
    /// 启动 Inno 安装器执行静默升级。
    /// 参数说明：/VERYSILENT 无界面，/SUPPRESSMSGBOXES 抑制意外弹窗，
    /// /NORESTART 禁止重启系统，/CLOSEAPPLICATIONS 允许安装器关闭占用文件的进程，
    /// /LOG 留下安装日志便于排障。
    /// </summary>
    /// <returns>是否成功启动安装器。</returns>
    public bool LaunchInstaller(string installerPath)
    {
        if (!File.Exists(installerPath))
        {
            Log($"安装器不存在：{installerPath}");
            return false;
        }

        var logPath = Path.Combine(AppDataRoot, "logs", "install.log");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = $"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /LOG=\"{logPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(installerPath) ?? UpdatesDirectory,
            };

            var process = Process.Start(startInfo);
            if (process is null)
            {
                Log("安装器启动失败：Process.Start 返回 null");
                return false;
            }

            Log($"已启动安装器 PID={process.Id}");
            return true;
        }
        catch (Exception ex)
        {
            Log($"启动安装器异常：{ex}");
            return false;
        }
    }

    /// <summary>写更新日志（追加，超过 512KB 时轮转为 .old）。失败静默。</summary>
    public static void Log(string message)
    {
        lock (LogLock)
        {
            try
            {
                var directory = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var file = new FileInfo(LogPath);
                if (file.Exists && file.Length > 512 * 1024)
                {
                    var old = LogPath + ".old";
                    if (File.Exists(old))
                    {
                        File.Delete(old);
                    }

                    File.Move(LogPath, old);
                }

                File.AppendAllText(
                    LogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
            catch
            {
                // 日志失败绝不影响主流程
            }
        }
    }

    private static void CleanupDownloads(SemanticVersion keepVersion)
    {
        try
        {
            if (!Directory.Exists(UpdatesDirectory))
            {
                return;
            }

            var keepName = $"{GitHubReleaseParser.AssetNamePrefix}{keepVersion}.exe";

            foreach (var file in Directory.EnumerateFiles(UpdatesDirectory))
            {
                var name = Path.GetFileName(file);
                var isCurrentTarget = string.Equals(name, keepName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, keepName + ".part", StringComparison.OrdinalIgnoreCase);

                if (!isCurrentTarget)
                {
                    TryDelete(file);
                }
            }
        }
        catch (Exception ex)
        {
            Log($"清理旧下载文件失败：{ex.Message}");
        }
    }

    private static void EnsureDiskSpace(long requiredBytes)
    {
        try
        {
            var root = Path.GetPathRoot(UpdatesDirectory);
            if (string.IsNullOrEmpty(root))
            {
                return;
            }

            var available = new DriveInfo(root).AvailableFreeSpace;

            // 预留 2 倍空间：下载中的 .part 与改名后的 .exe 在极端情况下会同时存在
            if (requiredBytes > 0 && available < requiredBytes * 2)
            {
                throw new IOException(
                    $"磁盘空间不足：需要约 {requiredBytes * 2 / 1024 / 1024} MB，当前可用 {available / 1024 / 1024} MB。");
            }
        }
        catch (IOException)
        {
            throw;
        }
        catch
        {
            // 取不到磁盘信息时不阻断（例如网络驱动器）
        }
    }

    private static void PruneBackups(int keep)
    {
        try
        {
            if (!Directory.Exists(BackupsDirectory))
            {
                return;
            }

            var directories = new DirectoryInfo(BackupsDirectory)
                .EnumerateDirectories()
                .OrderByDescending(d => d.Name, StringComparer.Ordinal)
                .Skip(keep)
                .ToList();

            foreach (var directory in directories)
            {
                try
                {
                    directory.Delete(recursive: true);
                }
                catch
                {
                    // 单个目录删不掉不影响其他
                }
            }
        }
        catch (Exception ex)
        {
            Log($"清理旧备份失败：{ex.Message}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 删除失败留待下次清理
        }
    }
}
