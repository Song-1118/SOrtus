using Microsoft.Data.Sqlite;
using SOrtus.Core.Models;
using SOrtus.Repositories;
using Xunit;

namespace SOrtus.UnitTests;

/// <summary>
/// 立项仓储集成测试：基于临时 SQLite 数据库文件，验证迁移、CRUD 与查询（验收 A4 / A5 / A9 / A10）。
/// 每个测试实例使用独立库文件，并在结束时清空连接池、删除文件，避免用例互相锁定。
/// </summary>
public class ProjectRepositoryTests : IDisposable
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"sortus_unit_{Guid.NewGuid():N}.db");

    public ProjectRepositoryTests()
    {
        Database.OverrideDbPath = _dbPath;
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite 默认启用连接池，连接释放后句柄仍被池持有，需显式清空才能删除文件。
        SqliteConnection.ClearAllPools();
        Database.OverrideDbPath = null;
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch (IOException)
        {
            // 临时文件清理失败不影响断言结果，交给系统临时目录回收。
        }
    }

    [Fact]
    public async Task Migration_CreatesSchema_AndProjectCurdRoundTrips()
    {
        await MigrationRunner.RunAsync();

        var repository = new ProjectRepository();

        // Create
        var created = await repository.AddAsync(new Project
        {
            Name = "SOrtus 测试",
            Type = ProjectType.Programming,
            Status = ProjectStatus.Planning,
            Summary = "单元测试用",
            TechStack = "WPF;.NET 10",
        });
        Assert.True(created.Id > 0);

        // Read by id
        var byId = await repository.GetByIdAsync(created.Id);
        Assert.NotNull(byId);
        Assert.Equal("SOrtus 测试", byId!.Name);
        Assert.Equal(ProjectType.Programming, byId.Type);
        Assert.Equal(2, byId.TechStackItems.Count);

        // Update
        byId.Status = ProjectStatus.Active;
        byId.ProgressPercent = 42;
        await repository.UpdateAsync(byId);
        var updated = await repository.GetByIdAsync(created.Id);
        Assert.Equal(ProjectStatus.Active, updated!.Status);
        Assert.Equal(42, updated.ProgressPercent);

        // Query by type
        var programming = await repository.QueryAsync(ProjectType.Programming, null, null);
        Assert.Contains(programming, p => p.Id == created.Id);

        // Delete
        await repository.DeleteAsync(created.Id);
        var afterDelete = await repository.GetByIdAsync(created.Id);
        Assert.Null(afterDelete);
    }

    /// <summary>回归用例：未填工期（可空日期）时写入历史版本会抛 "Value must be set."，导致立项直接失败。</summary>
    [Fact]
    public async Task Create_WithoutScheduleDates_PersistsNullsAndStrings()
    {
        await MigrationRunner.RunAsync();
        var repository = new ProjectRepository();

        var created = await repository.AddAsync(new Project
        {
            Name = "无工期项目",
            Type = ProjectType.Programming,
            Status = ProjectStatus.Planning,
        });

        Assert.True(created.Id > 0);

        var loaded = await repository.GetByIdAsync(created.Id);
        Assert.NotNull(loaded);
        Assert.Null(loaded!.ScheduleStart);
        Assert.Null(loaded.ScheduleEnd);
        Assert.Equal(string.Empty, loaded.Summary);
        Assert.Equal(string.Empty, loaded.TechStack);
    }

    [Fact]
    public async Task Create_WithScheduleDates_RoundTripsExactly()
    {
        await MigrationRunner.RunAsync();
        var repository = new ProjectRepository();

        var start = new DateTime(2026, 3, 1, 9, 30, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 6, 30, 18, 0, 0, DateTimeKind.Utc);

        var created = await repository.AddAsync(new Project
        {
            Name = "有工期项目",
            Type = ProjectType.Programming,
            ScheduleStart = start,
            ScheduleEnd = end,
        });

        var loaded = await repository.GetByIdAsync(created.Id);
        Assert.Equal(start, loaded!.ScheduleStart);
        Assert.Equal(end, loaded.ScheduleEnd);
    }

    [Fact]
    public async Task Repository_SupportsMultipleProjects_AndSearch()
    {
        await MigrationRunner.RunAsync();
        var repository = new ProjectRepository();
        await repository.AddAsync(new Project { Name = "Alpha 项目", Type = ProjectType.Programming, TechStack = "WPF;SQLite" });
        await repository.AddAsync(new Project { Name = "Beta 剪辑", Type = ProjectType.Clip });

        var all = await repository.QueryAsync(null, null, null);
        Assert.Equal(2, all.Count);

        var search = await repository.QueryAsync(null, null, "Beta");
        Assert.Single(search);
        Assert.Equal(ProjectType.Clip, search[0].Type);

        // 关键字覆盖技术栈列（PRD：列表可按技术栈检索）
        var byTechStack = await repository.QueryAsync(null, null, "SQLite");
        Assert.Single(byTechStack);
        Assert.Equal("Alpha 项目", byTechStack[0].Name);

        // 按状态筛选
        Assert.Empty(await repository.QueryAsync(null, ProjectStatus.Completed, null));
    }

    [Fact]
    public async Task Update_PersistsPostCreationFields_AndProgress()
    {
        await MigrationRunner.RunAsync();
        var repository = new ProjectRepository();

        var project = await repository.AddAsync(new Project { Name = "过程管理", Type = ProjectType.Programming });
        project.DevPlan = "里程碑一";
        project.Constraints = "不引入云服务";
        project.Goals = "跑通闭环";
        project.AcceptanceCriteria = "A1-A16 全绿";
        project.ProgressPercent = 66.5;
        project.Status = ProjectStatus.Active;
        await repository.UpdateAsync(project);

        var reloaded = await repository.GetByIdAsync(project.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("里程碑一", reloaded!.DevPlan);
        Assert.Equal("不引入云服务", reloaded.Constraints);
        Assert.Equal("跑通闭环", reloaded.Goals);
        Assert.Equal("A1-A16 全绿", reloaded.AcceptanceCriteria);
        Assert.Equal(66.5, reloaded.ProgressPercent);
        Assert.Equal(ProjectStatus.Active, reloaded.Status);
    }

    [Fact]
    public async Task Migration_IsIdempotent_AndKeepsExistingData()
    {
        await MigrationRunner.RunAsync();
        var repository = new ProjectRepository();
        await repository.AddAsync(new Project { Name = "迁移前数据", Type = ProjectType.Programming });

        // 二次运行迁移不应重复建表或清空数据
        await MigrationRunner.RunAsync();

        var all = await repository.QueryAsync(null, null, null);
        Assert.Single(all);
        Assert.Equal("迁移前数据", all[0].Name);
    }
}
