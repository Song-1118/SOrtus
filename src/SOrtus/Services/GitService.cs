using System.Globalization;
using System.IO;
using SOrtus.Core.Services;

namespace SOrtus.Services;

/// <summary>
/// Git 集成服务实现（规范 1.5：分支查看 / 远端配置 / 推送）。
/// <para>
/// 只做「调用 git + 解析输出」，不缓存状态；解析全部抽成静态方法，便于用假执行器做零进程单测。
/// 字段分隔符使用 <c>\u001F</c>（单元分隔符），避免提交信息里的 <c>|</c> 破坏列解析。
/// </para>
/// </summary>
internal sealed class GitService : IGitService
{
    private const char FieldSeparator = '\u001F';

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    private readonly IGitCommandRunner _runner;

    public GitService(IGitCommandRunner? runner = null)
    {
        _runner = runner ?? new GitProcessRunner();
    }

    public async Task<GitRepositoryInfo> InspectAsync(string folderPath, int commitCount = 30)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return GitRepositoryInfo.NotARepository;
        }

        var root = await _runner.RunAsync(folderPath, ["rev-parse", "--show-toplevel"], DefaultTimeout);
        if (root.ExitCode != 0 || string.IsNullOrWhiteSpace(root.StdOut))
        {
            return GitRepositoryInfo.NotARepository;
        }

        var workTree = root.StdOut.Trim();

        var branchResult = await _runner.RunAsync(workTree, ["rev-parse", "--abbrev-ref", "HEAD"], DefaultTimeout);
        var currentBranch = branchResult.StdOut.Trim();
        if (string.Equals(currentBranch, "HEAD", StringComparison.Ordinal))
        {
            // detached HEAD：用短 SHA 作为当前「分支」展示
            var sha = await _runner.RunAsync(workTree, ["rev-parse", "--short", "HEAD"], DefaultTimeout);
            currentBranch = sha.ExitCode == 0 ? sha.StdOut.Trim() : string.Empty;
        }

        var branchesResult = await _runner.RunAsync(
            workTree,
            [
                "for-each-ref",
                $"--format=%(refname){FieldSeparator}%(objectname:short){FieldSeparator}%(contents:subject){FieldSeparator}%(committerdate:iso-strict)",
                "refs/heads",
                "refs/remotes",
            ],
            DefaultTimeout);

        var commitsResult = await _runner.RunAsync(
            workTree,
            [
                "log",
                "-n",
                Math.Max(1, commitCount).ToString(CultureInfo.InvariantCulture),
                "--date=iso-strict",
                $"--format=%H{FieldSeparator}%h{FieldSeparator}%an{FieldSeparator}%ad{FieldSeparator}%s{FieldSeparator}%D",
            ],
            DefaultTimeout);

        var remotesResult = await _runner.RunAsync(workTree, ["remote", "-v"], DefaultTimeout);

        var aheadResult = await _runner.RunAsync(
            workTree,
            ["rev-list", "--left-right", "--count", "HEAD...@{u}"],
            DefaultTimeout);

        var hasUpstream = aheadResult.ExitCode == 0;
        var (ahead, behind) = hasUpstream
            ? ParseAheadBehind(aheadResult.StdOut)
            : (0, 0);

        var branches = ParseBranches(branchesResult.StdOut, currentBranch, ahead, behind);
        var commits = ParseCommits(commitsResult.StdOut);
        var remotes = ParseRemotes(remotesResult.StdOut);

        return new GitRepositoryInfo(
            IsRepository: true,
            WorkTreeRoot: workTree,
            CurrentBranch: currentBranch,
            HasRemote: remotes.Count > 0,
            HasUpstream: hasUpstream,
            Ahead: ahead,
            Behind: behind,
            Branches: branches,
            RecentCommits: commits,
            Remotes: remotes);
    }

    public async Task<GitOperationResult> AddOrSetRemoteAsync(string folderPath, string name, string url)
    {
        if (!Directory.Exists(folderPath))
        {
            return Failure(GitFailureKind.NotARepository, "项目目录不存在");
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
        {
            return Failure(GitFailureKind.NoRemote, "远端名称与地址不能为空");
        }

        var existing = await _runner.RunAsync(folderPath, ["remote", "get-url", name], DefaultTimeout);
        var args = existing.ExitCode == 0
            ? new[] { "remote", "set-url", name, url }
            : ["remote", "add", name, url];

        var result = await _runner.RunAsync(folderPath, args, DefaultTimeout);
        return result.ExitCode == 0 ? GitOperationResult.Ok() : Failure(result);
    }

    public async Task<GitOperationResult> PushAsync(string folderPath, string branch, string remote, bool setUpstream)
    {
        if (!Directory.Exists(folderPath))
        {
            return Failure(GitFailureKind.NotARepository, "项目目录不存在");
        }

        if (string.IsNullOrWhiteSpace(remote))
        {
            return Failure(GitFailureKind.NoRemote, "尚未配置远端仓库");
        }

        if (string.IsNullOrWhiteSpace(branch))
        {
            return Failure(GitFailureKind.Unknown, "无法确定当前分支");
        }

        var args = new List<string> { "push" };
        if (setUpstream)
        {
            args.Add("-u");
        }

        args.Add(remote);
        args.Add(branch);

        var result = await _runner.RunAsync(folderPath, args, DefaultTimeout);
        return result.ExitCode == 0 ? GitOperationResult.Ok() : Failure(result);
    }

    /// <summary>解析 <c>for-each-ref</c> 输出为分支列表。</summary>
    internal static IReadOnlyList<GitBranchInfo> ParseBranches(
        string stdout,
        string currentBranch,
        int ahead,
        int behind)
    {
        var branches = new List<GitBranchInfo>();
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return branches;
        }

        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(FieldSeparator);
            if (parts.Length < 4)
            {
                continue;
            }

            var refName = parts[0];
            var isRemote = refName.StartsWith("refs/remotes/", StringComparison.Ordinal);
            var name = refName.StartsWith("refs/heads/", StringComparison.Ordinal)
                ? refName["refs/heads/".Length..]
                : isRemote
                    ? refName["refs/remotes/".Length..]
                    : refName;

            if (name.EndsWith("/HEAD", StringComparison.Ordinal))
            {
                continue;
            }

            var isCurrent = !isRemote && string.Equals(name, currentBranch, StringComparison.Ordinal);

            branches.Add(new GitBranchInfo(
                Name: name,
                IsCurrent: isCurrent,
                IsRemote: isRemote,
                TipSha: parts[1],
                TipSubject: parts[2],
                TipWhen: ParseDate(parts[3]),
                Ahead: isCurrent ? ahead : 0,
                Behind: isCurrent ? behind : 0));
        }

        // 本地分支优先、当前分支最前
        return branches
            .OrderBy(b => b.IsRemote)
            .ThenByDescending(b => b.IsCurrent)
            .ThenBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>解析 <c>log</c> 输出为提交列表（首条即时间线最新）。</summary>
    internal static IReadOnlyList<GitCommitInfo> ParseCommits(string stdout)
    {
        var commits = new List<GitCommitInfo>();
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return commits;
        }

        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(FieldSeparator);
            if (parts.Length < 6)
            {
                continue;
            }

            commits.Add(new GitCommitInfo(
                Sha: parts[0],
                ShortSha: parts[1],
                Author: parts[2],
                When: ParseDate(parts[3]),
                Subject: parts[4],
                Refs: ParseRefs(parts[5])));
        }

        return commits;
    }

    /// <summary>解析 <c>remote -v</c> 输出（同名多行只保留 fetch 行）。</summary>
    internal static IReadOnlyList<GitRemoteInfo> ParseRemotes(string stdout)
    {
        var remotes = new List<GitRemoteInfo>();
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return remotes;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.Contains("(fetch)", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var url = parts[1].Replace(" (fetch)", string.Empty, StringComparison.Ordinal).Trim();
            if (seen.Add(parts[0]))
            {
                remotes.Add(new GitRemoteInfo(parts[0], url));
            }
        }

        return remotes;
    }

    /// <summary>解析 <c>rev-list --left-right --count</c>（左 = ahead，右 = behind）。</summary>
    internal static (int Ahead, int Behind) ParseAheadBehind(string stdout)
    {
        var parts = stdout.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return (0, 0);
        }

        _ = int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ahead);
        _ = int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var behind);
        return (Math.Max(0, ahead), Math.Max(0, behind));
    }

    /// <summary>把命令执行结果翻译成失败分类 + 可读消息。</summary>
    internal static GitOperationResult ClassifyFailure(GitCommandResult result)
    {
        if (result.ExitCode == -2 ||
            result.StdErr.Contains("GIT_NOT_INSTALLED", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(GitFailureKind.NotInstalled, "未检测到 git 命令行，请先安装 Git 并确保其在 PATH 中");
        }

        if (result.TimedOut)
        {
            return Failure(GitFailureKind.Timeout, "Git 命令超时，请检查网络或远端地址");
        }

        var stderr = result.StdErr ?? string.Empty;

        if (Contains(stderr, "could not read Username", "Authentication failed", "Permission denied (publickey)", "invalid username or password", "403"))
        {
            return Failure(GitFailureKind.AuthFailed, "远端认证失败，请先在本机 Git 中配置凭据或 SSH 密钥");
        }

        if (Contains(stderr, "non-fast-forward", "fetch first", "rejected", "updates were rejected"))
        {
            return Failure(GitFailureKind.NonFastForward, "远端存在新提交，请先在外部拉取后再推送");
        }

        if (Contains(stderr, "CONFLICT", "merge conflict"))
        {
            return Failure(GitFailureKind.Conflict, "存在未解决的冲突，请先处理冲突");
        }

        if (Contains(stderr, "does not appear to be a git repository", "not a git repo", "not a git repository"))
        {
            return Failure(GitFailureKind.NotARepository, "关联目录不是 Git 仓库");
        }

        if (Contains(stderr, "No configured push destination", "no upstream", "does not have any commits yet", "No such remote"))
        {
            return Failure(GitFailureKind.NoRemote, "尚未配置远端仓库，或当前分支没有上游分支");
        }

        return Failure(GitFailureKind.Unknown, FirstLine(stderr));
    }

    private static GitOperationResult Failure(GitCommandResult result) => ClassifyFailure(result);

    private static GitOperationResult Failure(GitFailureKind kind, string message) =>
        new(false, kind, message, string.Empty);

    private static bool Contains(string text, params string[] keywords) =>
        keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    private static string FirstLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Git 操作失败，未返回错误信息";
        }

        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? text.Trim();
        return line.Length <= 300 ? line : line[..300];
    }

    private static IReadOnlyList<string> ParseRefs(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(r => r.Replace("HEAD -> ", string.Empty, StringComparison.Ordinal)
                         .Replace("tag: ", string.Empty, StringComparison.Ordinal)
                         .Trim())
            .Where(r => r.Length > 0)
            .ToList();
    }

    private static DateTimeOffset ParseDate(string text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value)
            ? value
            : DateTimeOffset.MinValue;
}
