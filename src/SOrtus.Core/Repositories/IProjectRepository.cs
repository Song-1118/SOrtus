using SOrtus.Core.Models;

namespace SOrtus.Core.Repositories;

/// <summary>
/// 立项仓储契约（数据访问层接口）。
/// 实现位于 SOrtus 主程序的 Repositories/ProjectRepository。
/// </summary>
public interface IProjectRepository
{
    /// <summary>获取全部立项。</summary>
    Task<IReadOnlyList<Project>> GetAllAsync();

    /// <summary>按条件查询（类型 / 状态 / 关键字）。</summary>
    Task<IReadOnlyList<Project>> QueryAsync(ProjectType? type, ProjectStatus? status, string? keyword);

    /// <summary>按 Id 获取立项。</summary>
    Task<Project?> GetByIdAsync(int id);

    /// <summary>新增立项，返回带 Id 的实体。</summary>
    Task<Project> AddAsync(Project project);

    /// <summary>更新立项。</summary>
    Task UpdateAsync(Project project);

    /// <summary>删除立项。</summary>
    Task DeleteAsync(int id);
}
