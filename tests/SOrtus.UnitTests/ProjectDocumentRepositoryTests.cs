using Microsoft.Data.Sqlite;
using SOrtus.Core.Models;
using SOrtus.Repositories;
using Xunit;

namespace SOrtus.UnitTests;

/// <summary>
/// 项目文档仓储集成测试：基于临时 SQLite 数据库文件，验证版本列、文档 CRUD、
/// PRD 单槽位约束、排序号分配与外键级联删除。
/// 每个测试实例使用独立库文件，并在结束时清空连接池、删除文件，避免用例互相锁定。
/// </summary>
public class ProjectDocumentRepositoryTests : IDisposable
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"sortus_doc_unit_{Guid.NewGuid():N}.db");

    private static readonly DateTime FixedCreatedAt = new(2000, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    public ProjectDocumentRepositoryTests()
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

    /// <summary>① 立项 Version 列可写可读，默认空串且不破坏既有字段映射。</summary>
    [Fact]
    public async Task ProjectVersion_DefaultsToEmpty_AndRoundTrips()
    {
        await MigrationRunner.RunAsync();
        var repository = new ProjectRepository();

        var created = await repository.AddAsync(new Project
        {
            Name = "版本号项目",
            Type = ProjectType.Programming,
            Status = ProjectStatus.Planning,
        });
        Assert.Equal(string.Empty, created.Version);

        var loaded = await repository.GetByIdAsync(created.Id);
        Assert.NotNull(loaded);
        Assert.Equal(string.Empty, loaded!.Version);

        loaded.Version = "1.2.0";
        await repository.UpdateAsync(loaded);

        var reloaded = await repository.GetByIdAsync(created.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("1.2.0", reloaded!.Version);
        Assert.Equal("版本号项目", reloaded.Name);
    }

    /// <summary>② Other 文档：新增 → 查询 → 改内容 → 删除 全链路。</summary>
    [Fact]
    public async Task OtherDocument_AddQueryUpdateAndDelete_RoundTrips()
    {
        await MigrationRunner.RunAsync();
        var projects = new ProjectRepository();
        var documents = new ProjectDocumentRepository();

        var project = await projects.AddAsync(new Project { Name = "文档项目", Type = ProjectType.Programming });

        var documentId = await documents.UpsertAsync(new ProjectDocument
        {
            ProjectId = project.Id,
            Category = DocumentCategory.Other,
            Title = "设计备忘",
            SourceFileName = "design.md",
            Format = "md",
            Content = "# 初版标题",
            SortOrder = await documents.GetNextSortOrderAsync(project.Id),
            CreatedAt = FixedCreatedAt,
            UpdatedAt = FixedCreatedAt,
        });
        Assert.True(documentId > 0);

        var loaded = await documents.GetByProjectAsync(project.Id);
        Assert.Single(loaded);
        Assert.Equal("设计备忘", loaded[0].Title);
        Assert.Equal("design.md", loaded[0].SourceFileName);
        Assert.Equal("md", loaded[0].Format);
        Assert.Equal(DocumentCategory.Other, loaded[0].Category);
        Assert.Equal(FixedCreatedAt, loaded[0].CreatedAt);
        Assert.Equal(FixedCreatedAt, loaded[0].UpdatedAt);

        Assert.True(await documents.UpdateContentAsync(documentId, "# 修订标题"));
        var updated = await documents.GetByProjectAsync(project.Id);
        Assert.Single(updated);
        Assert.Equal("# 修订标题", updated[0].Content);
        Assert.True(updated[0].UpdatedAt > FixedCreatedAt);

        Assert.True(await documents.DeleteAsync(documentId));
        Assert.Empty(await documents.GetByProjectAsync(project.Id));
        Assert.False(await documents.DeleteAsync(documentId));
    }

    /// <summary>③ PRD 槽位连续 Upsert 两次只保留 1 行，内容为第二次写入。</summary>
    [Fact]
    public async Task PrdSlot_UpsertTwice_KeepsSingleRowWithLatestContent()
    {
        await MigrationRunner.RunAsync();
        var projects = new ProjectRepository();
        var documents = new ProjectDocumentRepository();

        var project = await projects.AddAsync(new Project { Name = "PRD 项目", Type = ProjectType.Programming });

        var firstId = await documents.UpsertAsync(new ProjectDocument
        {
            ProjectId = project.Id,
            Category = DocumentCategory.Prd,
            Title = "产品需求文档",
            Format = "md",
            Content = "第一版需求",
            CreatedAt = FixedCreatedAt,
            UpdatedAt = FixedCreatedAt,
        });

        // 第二次 Id 仍为 0（模拟上层未持有主键的场景），应命中槽位并转为更新
        var secondId = await documents.UpsertAsync(new ProjectDocument
        {
            ProjectId = project.Id,
            Category = DocumentCategory.Prd,
            Title = "产品需求文档",
            Format = "md",
            Content = "第二版需求",
            CreatedAt = FixedCreatedAt,
            UpdatedAt = FixedCreatedAt.AddHours(1),
        });

        Assert.Equal(firstId, secondId);

        var all = await documents.GetByProjectAsync(project.Id);
        Assert.Single(all);
        Assert.Equal("第二版需求", all[0].Content);
        Assert.Equal(DocumentCategory.Prd, all[0].Category);

        var slot = await documents.GetSlotAsync(project.Id, DocumentCategory.Prd);
        Assert.NotNull(slot);
        Assert.Equal("第二版需求", slot!.Content);
    }

    /// <summary>④ GetNextSortOrderAsync 随插入递增；无文档时从 0 开始。</summary>
    [Fact]
    public async Task GetNextSortOrder_IncrementsAfterEachInsert()
    {
        await MigrationRunner.RunAsync();
        var projects = new ProjectRepository();
        var documents = new ProjectDocumentRepository();

        var project = await projects.AddAsync(new Project { Name = "排序项目", Type = ProjectType.Programming });

        Assert.Equal(0, await documents.GetNextSortOrderAsync(project.Id));

        var firstOrder = await documents.GetNextSortOrderAsync(project.Id);
        await documents.UpsertAsync(new ProjectDocument
        {
            ProjectId = project.Id,
            Category = DocumentCategory.Other,
            Title = "文档一",
            SortOrder = firstOrder,
            CreatedAt = FixedCreatedAt,
            UpdatedAt = FixedCreatedAt,
        });

        var secondOrder = await documents.GetNextSortOrderAsync(project.Id);
        Assert.Equal(firstOrder + 1, secondOrder);

        await documents.UpsertAsync(new ProjectDocument
        {
            ProjectId = project.Id,
            Category = DocumentCategory.Other,
            Title = "文档二",
            SortOrder = secondOrder,
            CreatedAt = FixedCreatedAt,
            UpdatedAt = FixedCreatedAt,
        });

        var thirdOrder = await documents.GetNextSortOrderAsync(project.Id);
        Assert.Equal(secondOrder + 1, thirdOrder);

        var ordered = await documents.GetByProjectAsync(project.Id);
        Assert.Equal(new[] { "文档一", "文档二" }, ordered.Select(d => d.Title).ToArray());
    }

    /// <summary>⑤ 删除立项后其文档级联归零（验证 PRAGMA foreign_keys 生效）。</summary>
    [Fact]
    public async Task DeletingProject_CascadesToDocuments()
    {
        await MigrationRunner.RunAsync();
        var projects = new ProjectRepository();
        var documents = new ProjectDocumentRepository();

        var project = await projects.AddAsync(new Project { Name = "级联项目", Type = ProjectType.Programming });

        await documents.UpsertAsync(new ProjectDocument
        {
            ProjectId = project.Id,
            Category = DocumentCategory.Prd,
            Title = "需求",
            Content = "# 需求",
            CreatedAt = FixedCreatedAt,
            UpdatedAt = FixedCreatedAt,
        });
        await documents.UpsertAsync(new ProjectDocument
        {
            ProjectId = project.Id,
            Category = DocumentCategory.Other,
            Title = "附录",
            Content = "附录内容",
            CreatedAt = FixedCreatedAt,
            UpdatedAt = FixedCreatedAt,
        });

        Assert.Equal(2, (await documents.GetByProjectAsync(project.Id)).Count);

        await projects.DeleteAsync(project.Id);

        Assert.Empty(await documents.GetByProjectAsync(project.Id));
        Assert.Null(await documents.GetSlotAsync(project.Id, DocumentCategory.Prd));
    }

    /// <summary>⑥ 更新不存在的文档返回 false（详情页勾选待办时的兜底分支）。</summary>
    [Fact]
    public async Task UpdateContent_UnknownId_ReturnsFalse()
    {
        await MigrationRunner.RunAsync();
        var documents = new ProjectDocumentRepository();

        Assert.False(await documents.UpdateContentAsync(987654, "新内容"));
    }

    /// <summary>⑦ 勾选待办后的正文回写：再次读取能拿到新内容与新的更新时间。</summary>
    [Fact]
    public async Task UpdateContent_PersistsMarkdownSource()
    {
        await MigrationRunner.RunAsync();
        var projects = new ProjectRepository();
        var documents = new ProjectDocumentRepository();
        var project = await projects.AddAsync(new Project { Name = "勾选项目", Type = ProjectType.Programming });

        var id = await documents.UpsertAsync(new ProjectDocument
        {
            ProjectId = project.Id,
            Category = DocumentCategory.Other,
            Title = "待办清单",
            Content = "- [ ] 未完成\n- [ ] 第二条\n",
            CreatedAt = FixedCreatedAt,
            UpdatedAt = FixedCreatedAt,
        });

        const string updated = "- [x] 未完成\n- [ ] 第二条\n";
        Assert.True(await documents.UpdateContentAsync(id, updated));

        var reloaded = (await documents.GetByProjectAsync(project.Id)).Single(d => d.Id == id);
        Assert.Equal(updated, reloaded.Content);
        Assert.True(reloaded.UpdatedAt > FixedCreatedAt);
    }
}
