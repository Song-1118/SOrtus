using SOrtus.Core.Models;
using SOrtus.Core.Repositories;

namespace SOrtus.Services;

/// <summary>
/// 数据导出/导入/备份服务（规范：全量/单立项 .json 导出、导入、备份恢复）。
/// 范围属于 1.4（0.5.0）版本，本 MVP 仅预留模块，实现见后续版本。
/// 序列化基于对象模型映射为 JSON，避免直接导出数据库二进制。
/// </summary>
internal sealed class DataExportService
{
    private readonly IProjectRepository _repository;

    public DataExportService(IProjectRepository repository)
    {
        _repository = repository;
    }

    // TODO(1.4): 实现 ExportAll / ExportSingle / Import(覆盖|合并) / Backup / Restore。
}
