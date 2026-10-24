using SOrtus.Core.Models;

namespace SOrtus.Core.Services;

/// <summary>
/// 立项业务服务契约（业务逻辑层接口）。
/// 负责立项 CRUD 协调、时间戳维护等，不依赖具体 UI 或持久化实现。
/// </summary>
public interface IProjectService
{
    /// <summary>获取全部立项。</summary>
    Task<IReadOnlyList<Project>> GetAllAsync();

    /// <summary>按条件查询。</summary>
    Task<IReadOnlyList<Project>> QueryAsync(ProjectType? type, ProjectStatus? status, string? keyword);

    /// <summary>获取单个立项。</summary>
    Task<Project?> GetByIdAsync(int id);

    /// <summary>创建立项（自动填充创建/更新时间）。</summary>
    Task<Project> CreateAsync(Project project);

    /// <summary>保存立项（新增或更新）。</summary>
    Task<Project> SaveAsync(Project project);

    /// <summary>删除立项。</summary>
    Task DeleteAsync(int id);
}
