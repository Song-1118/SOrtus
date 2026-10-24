namespace SOrtus.Core.Services;

/// <summary>
/// Git 操作失败分类（用于把命令行 stderr 翻译成用户能看懂的原因）。
/// </summary>
public enum GitFailureKind
{
    /// <summary>成功。</summary>
    None,

    /// <summary>本机未安装 git 或不在 PATH 中。</summary>
    NotInstalled,

    /// <summary>关联目录不是 Git 仓库。</summary>
    NotARepository,

    /// <summary>仓库未配置任何远端。</summary>
    NoRemote,

    /// <summary>当前分支没有上游分支。</summary>
    NoUpstream,

    /// <summary>认证失败（凭据 / 公钥 / 权限）。</summary>
    AuthFailed,

    /// <summary>远端存在新提交，非快进推送被拒绝。</summary>
    NonFastForward,

    /// <summary>存在冲突。</summary>
    Conflict,

    /// <summary>命令超时。</summary>
    Timeout,

    /// <summary>未分类错误。</summary>
    Unknown,
}

/// <summary>一条提交记录（时间线节点）。</summary>
public sealed record GitCommitInfo(
    string Sha,
    string ShortSha,
    string Author,
    DateTimeOffset When,
    string Subject,
    IReadOnlyList<string> Refs);

/// <summary>一条分支（本地或远程）。</summary>
public sealed record GitBranchInfo(
    string Name,
    bool IsCurrent,
    bool IsRemote,
    string TipSha,
    string TipSubject,
    DateTimeOffset TipWhen,
    int Ahead,
    int Behind);

/// <summary>一个远端。</summary>
public sealed record GitRemoteInfo(string Name, string Url);

/// <summary>仓库快照（时间线 + 分支 + 远端 + 领先/落后）。</summary>
public sealed record GitRepositoryInfo(
    bool IsRepository,
    string WorkTreeRoot,
    string CurrentBranch,
    bool HasRemote,
    bool HasUpstream,
    int Ahead,
    int Behind,
    IReadOnlyList<GitBranchInfo> Branches,
    IReadOnlyList<GitCommitInfo> RecentCommits,
    IReadOnlyList<GitRemoteInfo> Remotes)
{
    /// <summary>非 Git 仓库的空快照。</summary>
    public static GitRepositoryInfo NotARepository { get; } = new(
        false,
        string.Empty,
        string.Empty,
        false,
        false,
        0,
        0,
        Array.Empty<GitBranchInfo>(),
        Array.Empty<GitCommitInfo>(),
        Array.Empty<GitRemoteInfo>());
}

/// <summary>一条 git 命令的原始执行结果。</summary>
public sealed record GitCommandResult(int ExitCode, string StdOut, string StdErr, bool TimedOut);

/// <summary>Git 操作结果（成功标记 + 失败分类 + 人话消息 + 原始输出）。</summary>
public sealed record GitOperationResult(bool Success, GitFailureKind Kind, string Message, string RawOutput)
{
    /// <summary>成功结果。</summary>
    public static GitOperationResult Ok(string message = "") => new(true, GitFailureKind.None, message, string.Empty);
}

/// <summary>
/// git 命令行执行器抽象（可注入，便于对解析与参数拼装做零进程单测）。
/// </summary>
public interface IGitCommandRunner
{
    /// <summary>在 <paramref name="workDir"/> 下执行一条 git 命令（<paramref name="args"/> 不含 "git" 本身）。</summary>
    Task<GitCommandResult> RunAsync(string workDir, IReadOnlyList<string> args, TimeSpan timeout);
}

/// <summary>
/// Git 集成服务（规范 1.5：编程类型的分支查看与推送）。
/// 实现走本机 git 命令行，不内置凭据管理；认证失败提示用户在系统 Git 中配置。
/// </summary>
public interface IGitService
{
    /// <summary>读取仓库快照（是否仓库 / 当前分支 / 分支列表 / 最近提交 / 远端 / 领先落后）。</summary>
    Task<GitRepositoryInfo> InspectAsync(string folderPath, int commitCount = 30);

    /// <summary>添加或更新远端地址（已存在同名远端时改为 set-url）。</summary>
    Task<GitOperationResult> AddOrSetRemoteAsync(string folderPath, string name, string url);

    /// <summary>推送当前分支到远端。</summary>
    Task<GitOperationResult> PushAsync(string folderPath, string branch, string remote, bool setUpstream);
}
