using SOrtus.Core.Services;
using SOrtus.Services;
using Xunit;

namespace SOrtus.UnitTests;

/// <summary>
/// Git 服务解析测试（详情页 V2 GIT 版块）：
/// 用假执行器喂固定 git 输出，验证快照解析、远端配置分支与失败分类，零进程、零网络。
/// </summary>
public class GitServiceTests
{
    private const char FS = '\u001F';

    private sealed class FakeGitRunner : IGitCommandRunner
    {
        private readonly Func<IReadOnlyList<string>, GitCommandResult> _handler;

        public FakeGitRunner(Func<IReadOnlyList<string>, GitCommandResult> handler)
        {
            _handler = handler;
        }

        public List<IReadOnlyList<string>> Calls { get; } = new();

        public Task<GitCommandResult> RunAsync(string workDir, IReadOnlyList<string> args, TimeSpan timeout)
        {
            Calls.Add(args.ToList());
            return Task.FromResult(_handler(args));
        }
    }

    private static GitCommandResult Ok(string stdout) => new(0, stdout, string.Empty, false);

    private static GitCommandResult Fail(string stderr, int code = 1) => new(code, string.Empty, stderr, false);

    private static FakeGitRunner CreateRunner(
        string branch = "master",
        bool hasUpstream = true,
        string remotes = "origin\thttps://example.com/sortus.git (fetch)\norigin\thttps://example.com/sortus.git (push)\n",
        string? commitLine = null)
    {
        var branches =
            $"refs/heads/master{FS}32fe807{FS}build: 版本号提升至 0.1.1{FS}2026-10-24T12:00:00+08:00\n" +
            $"refs/heads/feature/detail-v2{FS}a1b2c3d{FS}feat: 详情页三分版块{FS}2026-10-24T14:00:00+08:00\n" +
            $"refs/remotes/origin/master{FS}32fe807{FS}build: 版本号提升至 0.1.1{FS}2026-10-24T11:00:00+08:00\n" +
            $"refs/remotes/origin/HEAD{FS}32fe807{FS}build: 版本号提升至 0.1.1{FS}2026-10-24T11:00:00+08:00\n";

        var commits = commitLine ??
            $"aaaa1111{FS}32fe807{FS}Song-1118{FS}2026-10-24T12:00:00+08:00{FS}build: 版本号提升至 0.1.1{FS}HEAD -> master, origin/master\n" +
            $"bbbb2222{FS}df5fef5{FS}Song-1118{FS}2026-10-24T08:00:00+08:00{FS}feat(ui): 交互修复{FS}\n";

        return new FakeGitRunner(args =>
        {
            switch (args[0])
            {
                case "rev-parse" when args[1] == "--show-toplevel":
                    return Ok("/work/sortus\n");
                case "rev-parse" when args[1] == "--abbrev-ref":
                    return Ok(branch + "\n");
                case "rev-parse" when args[1] == "--short":
                    return Ok("a1b2c3d\n");
                case "for-each-ref":
                    return Ok(branches);
                case "log":
                    return Ok(commits);
                case "remote" when args[1] == "-v":
                    return Ok(remotes);
                case "remote" when args[1] == "get-url":
                    return Ok("https://example.com/old.git\n");
                case "rev-list" when hasUpstream:
                    return Ok("2\t1\n");
                case "rev-list":
                    return Fail("fatal: no upstream configured for branch 'master'", 128);
                default:
                    return Ok(string.Empty);
            }
        });
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sortus_git_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public async Task InspectAsync_ParsesFullSnapshot()
    {
        var folder = CreateTempDirectory();
        try
        {
            var service = new GitService(CreateRunner());

            var info = await service.InspectAsync(folder);

            Assert.True(info.IsRepository);
            Assert.Equal("master", info.CurrentBranch);
            Assert.True(info.HasRemote);
            Assert.True(info.HasUpstream);
            Assert.Equal(2, info.Ahead);
            Assert.Equal(1, info.Behind);

            // origin/HEAD 被跳过：2 个本地 + 1 个远程
            Assert.Equal(3, info.Branches.Count);
            Assert.True(info.Branches.Single(b => b.Name == "master").IsCurrent);
            Assert.True(info.Branches.Single(b => b.Name == "origin/master").IsRemote);

            Assert.Equal(2, info.RecentCommits.Count);
            Assert.Contains("master", info.RecentCommits[0].Refs);
            Assert.Contains("origin/master", info.RecentCommits[0].Refs);
            Assert.DoesNotContain(info.RecentCommits[0].Refs, r => r.StartsWith("HEAD ->", StringComparison.Ordinal));
            Assert.Empty(info.RecentCommits[1].Refs);
            Assert.Equal("build: 版本号提升至 0.1.1", info.RecentCommits[0].Subject);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task InspectAsync_DetachedHead_UsesShortSha()
    {
        var folder = CreateTempDirectory();
        try
        {
            var info = await new GitService(CreateRunner(branch: "HEAD")).InspectAsync(folder);

            Assert.Equal("a1b2c3d", info.CurrentBranch);
            Assert.True(info.HasRemote);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task InspectAsync_NoUpstream_DoesNotThrow()
    {
        var folder = CreateTempDirectory();
        try
        {
            var info = await new GitService(CreateRunner(hasUpstream: false)).InspectAsync(folder);

            Assert.True(info.IsRepository);
            Assert.False(info.HasUpstream);
            Assert.Equal(0, info.Ahead);
            Assert.Equal(0, info.Behind);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task InspectAsync_NoRemote_HasRemoteFalse()
    {
        var folder = CreateTempDirectory();
        try
        {
            var info = await new GitService(CreateRunner(remotes: string.Empty)).InspectAsync(folder);

            Assert.False(info.HasRemote);
            Assert.Empty(info.Remotes);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task InspectAsync_NonExistentFolder_SkipsCommands()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"sortus_missing_{Guid.NewGuid():N}");
        var runner = CreateRunner();

        var info = await new GitService(runner).InspectAsync(folder);

        Assert.False(info.IsRepository);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task AddOrSetRemote_UsesSetUrlWhenRemoteExists()
    {
        var folder = CreateTempDirectory();
        try
        {
            var runner = CreateRunner();
            var result = await new GitService(runner).AddOrSetRemoteAsync(folder, "origin", "https://example.com/new.git");

            Assert.True(result.Success);
            Assert.Equal(2, runner.Calls.Count);
            Assert.Equal("remote", runner.Calls[1][0]);
            Assert.Equal("set-url", runner.Calls[1][1]);
            Assert.Contains("https://example.com/new.git", runner.Calls[1]);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task AddOrSetRemote_UsesAddWhenRemoteMissing()
    {
        var folder = CreateTempDirectory();
        try
        {
            var runner = new FakeGitRunner(args => args[1] == "get-url"
                ? Fail("error: No such remote 'origin'", 2)
                : Ok(string.Empty));

            var result = await new GitService(runner).AddOrSetRemoteAsync(folder, "origin", "https://example.com/new.git");

            Assert.True(result.Success);
            Assert.Equal("add", runner.Calls[1][1]);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task PushAsync_SetUpstreamAddsMinusU()
    {
        var folder = CreateTempDirectory();
        try
        {
            var runner = CreateRunner();
            var result = await new GitService(runner).PushAsync(folder, "master", "origin", setUpstream: true);

            Assert.True(result.Success);
            var push = Assert.Single(runner.Calls);
            Assert.Equal(new[] { "push", "-u", "origin", "master" }, push.ToArray());
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task PushAsync_WithoutRemote_FailsFast()
    {
        var folder = CreateTempDirectory();
        try
        {
            var runner = CreateRunner();
            var result = await new GitService(runner).PushAsync(folder, "master", string.Empty, setUpstream: false);

            Assert.False(result.Success);
            Assert.Equal(GitFailureKind.NoRemote, result.Kind);
            Assert.Empty(runner.Calls);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Theory]
    [InlineData("remote: could not read Username for 'https://example.com': terminal prompts disabled", GitFailureKind.AuthFailed)]
    [InlineData(" ! [rejected] master -> master (non-fast-forward)", GitFailureKind.NonFastForward)]
    [InlineData("CONFLICT (content): Merge conflict in a.txt", GitFailureKind.Conflict)]
    [InlineData("fatal: not a git repository (or any of the parent directories)", GitFailureKind.NotARepository)]
    [InlineData("fatal: The current branch master has no upstream branch", GitFailureKind.NoRemote)]
    [InlineData("fatal: something unexpected", GitFailureKind.Unknown)]
    public void ClassifyFailure_MapsStderr(string stderr, GitFailureKind expected)
    {
        var result = GitService.ClassifyFailure(new GitCommandResult(1, string.Empty, stderr, false));

        Assert.False(result.Success);
        Assert.Equal(expected, result.Kind);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public void ClassifyFailure_MapsMissingGitAndTimeout()
    {
        Assert.Equal(GitFailureKind.NotInstalled,
            GitService.ClassifyFailure(new GitCommandResult(-2, string.Empty, "GIT_NOT_INSTALLED", false)).Kind);

        Assert.Equal(GitFailureKind.Timeout,
            GitService.ClassifyFailure(new GitCommandResult(-1, string.Empty, string.Empty, true)).Kind);
    }

    [Fact]
    public void ParseRemotes_DeduplicatesFetchAndPushLines()
    {
        var remotes = GitService.ParseRemotes(
            "origin\thttps://example.com/a.git (fetch)\norigin\thttps://example.com/a.git (push)\nupstream\thttps://example.com/b.git (fetch)\n");

        Assert.Equal(2, remotes.Count);
        Assert.Equal("https://example.com/a.git", remotes[0].Url);
        Assert.Equal("upstream", remotes[1].Name);
    }

    [Fact]
    public void ParseAheadBehind_HandlesGarbage()
    {
        Assert.Equal((0, 0), GitService.ParseAheadBehind(string.Empty));
        Assert.Equal((3, 1), GitService.ParseAheadBehind("3\t1\n"));
    }
}
