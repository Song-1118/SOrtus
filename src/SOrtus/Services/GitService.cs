using SOrtus.Core.Models;

namespace SOrtus.Services;

/// <summary>
/// Git 集成服务（规范：编程类型 Git 提交/推送/回退）。
/// 范围属于 1.5（0.6.0）版本，本 MVP 仅预留模块与契约，实现见后续版本。
/// 实现方式：优先 LibGit2Sharp，其次调用本地 git 命令行；不内置凭据管理。
/// </summary>
internal sealed class GitService
{
    // TODO(1.5): 实现 Commit / Push / Revert，含仓库合法性校验与错误提示。
    public bool IsGitRepository(string folderPath) => false;

    public Task<string> CommitAsync(Project project, string message) =>
        throw new NotImplementedException("Git operations land in version 1.5 (0.6.0).");
}
