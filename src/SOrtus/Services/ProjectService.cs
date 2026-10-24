using SOrtus.Core.Models;
using SOrtus.Core.Repositories;
using SOrtus.Core.Services;

namespace SOrtus.Services;

/// <summary>
/// 立项业务服务实现（业务逻辑层）。负责 CRUD 协调与时间戳维护。
/// 不直接访问 SQLite，仅依赖仓储接口（规范 6.3：跨层通信优先接口 + 依赖注入）。
/// </summary>
internal sealed class ProjectService : IProjectService
{
    private readonly IProjectRepository _repository;

    public ProjectService(IProjectRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<IReadOnlyList<Project>> GetAllAsync() => _repository.GetAllAsync();

    public Task<IReadOnlyList<Project>> QueryAsync(ProjectType? type, ProjectStatus? status, string? keyword) =>
        _repository.QueryAsync(type, status, keyword);

    public Task<Project?> GetByIdAsync(int id) => _repository.GetByIdAsync(id);

    public async Task<Project> CreateAsync(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var now = DateTime.UtcNow;
        project.CreatedAt = now;
        project.UpdatedAt = now;
        return await _repository.AddAsync(project);
    }

    public async Task<Project> SaveAsync(Project project)
    {
        var existing = await _repository.GetByIdAsync(project.Id);
        if (existing is null)
        {
            return await CreateAsync(project);
        }

        project.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(project);
        return project;
    }

    public Task DeleteAsync(int id) => _repository.DeleteAsync(id);
}
