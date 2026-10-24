using SOrtus.Core.Models;

namespace SOrtus.Core.Repositories;

/// <summary>
/// 项目文档仓储契约（数据访问层接口）。
/// 实现位于 SOrtus 主程序的 Repositories/ProjectDocumentRepository。
/// </summary>
public interface IProjectDocumentRepository
{
    /// <summary>按立项获取全部文档（按 SortOrder、Id 升序）。</summary>
    Task<IReadOnlyList<ProjectDocument>> GetByProjectAsync(int projectId);

    /// <summary>按「单槽位」分类获取文档（Prd / IterationPlan）；无则返回 null。</summary>
    Task<ProjectDocument?> GetSlotAsync(int projectId, DocumentCategory category);

    /// <summary>
    /// 保存文档（新增或更新），返回最终主键。
    /// Prd / IterationPlan 为单槽位分类：同一立项下重复保存只会保留一条记录。
    /// </summary>
    Task<int> UpsertAsync(ProjectDocument document);

    /// <summary>更新正文与更新时间，返回是否命中记录。</summary>
    Task<bool> UpdateContentAsync(int documentId, string content);

    /// <summary>删除文档，返回是否命中记录。</summary>
    Task<bool> DeleteAsync(int documentId);

    /// <summary>获取该立项下一个可用排序序号（无记录时返回 0）。</summary>
    Task<int> GetNextSortOrderAsync(int projectId);
}
