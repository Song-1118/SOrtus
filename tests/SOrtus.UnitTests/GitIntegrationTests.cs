using System.Diagnostics;
using System.Text;
using SOrtus.Services;
using Xunit;

namespace SOrtus.UnitTests;

/// <summary>
/// Git 集成测试：在临时目录真实创建仓库与裸远端，验证「探测 → 配远端 → 推送」全链路。
/// 机器上没有 git 时静默跳过（返回），不制造环境性红灯。
/// </summary>
public class GitIntegrationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task InspectConfigureAndPush_AgainstRealRepository()
    {
        if (!IsGitAvailable())
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), $"sortus_git_it_{Guid.NewGuid():N}");
        var work = Path.Combine(root, "work");
        var bare = Path.Combine(root, "remote.git");
        Directory.CreateDirectory(work);

        try
        {
            RunGit(work, "init");
            RunGit(work, "-c", "user.name=SOrtus QA", "-c", "user.email=qa@example.com", "commit", "--allow-empty", "-m", "first commit");
            RunGit(work, "-c", "user.name=SOrtus QA", "-c", "user.email=qa@example.com", "commit", "--allow-empty", "-m", "second commit");
            RunGit(work, "branch", "feature/detail-v2");

            RunGit(root, "init", "--bare", bare);

            var service = new GitService();

            var before = await service.InspectAsync(work);
            Assert.True(before.IsRepository);
            Assert.Equal(2, before.RecentCommits.Count);
            Assert.Contains(before.Branches, b => b.Name == "feature/detail-v2" && !b.IsRemote);
            Assert.False(before.HasRemote);

            var current = before.CurrentBranch;
            Assert.False(string.IsNullOrWhiteSpace(current));

            var added = await service.AddOrSetRemoteAsync(work, "origin", bare.Replace('\\', '/'));
            Assert.True(added.Success, added.Message);

            var afterRemote = await service.InspectAsync(work);
            Assert.True(afterRemote.HasRemote);
            Assert.Equal("origin", afterRemote.Remotes[0].Name);

            var pushed = await service.PushAsync(work, current, "origin", setUpstream: true);
            Assert.True(pushed.Success, pushed.Message);

            var afterPush = await service.InspectAsync(work);
            Assert.True(afterPush.HasUpstream);
            Assert.Equal(0, afterPush.Ahead);
            Assert.Equal(0, afterPush.Behind);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task InspectAsync_OnPlainFolder_ReturnsNotARepository()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"sortus_plain_{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            var info = await new GitService().InspectAsync(folder);

            Assert.False(info.IsRepository);
            Assert.Empty(info.RecentCommits);
        }
        finally
        {
            TryDelete(folder);
        }
    }

    private static bool IsGitAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process is null)
            {
                return false;
            }

            process.WaitForExit(5000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void RunGit(string workDir, params string[] args)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GIT_AUTHOR_NAME"] = "SOrtus QA";
        startInfo.Environment["GIT_AUTHOR_EMAIL"] = "qa@example.com";
        startInfo.Environment["GIT_COMMITTER_NAME"] = "SOrtus QA";
        startInfo.Environment["GIT_COMMITTER_EMAIL"] = "qa@example.com";

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException("无法启动 git 进程");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit((int)Timeout.TotalMilliseconds), $"git {string.Join(' ', args)} 超时");

        if (process.ExitCode != 0 && !stdout.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"git {string.Join(' ', args)} 失败：{stderr}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
