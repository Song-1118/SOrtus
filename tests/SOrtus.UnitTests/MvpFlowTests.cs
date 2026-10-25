using Microsoft.Data.Sqlite;
using SOrtus.Core.Models;
using SOrtus.Repositories;
using SOrtus.Services;
using SOrtus.ViewModels;
using Xunit;

namespace SOrtus.UnitTests;

/// <summary>
/// MVP 端到端闭环测试（验收 A5 / A8 / A9 / A10 / A11）：
/// 真实仓储 + 真实业务服务 + 真实视图模型，串起「向导创建 → 列表可见 → 详情编辑 → 模拟重启后仍在」整条路径。
/// </summary>
public class MvpFlowTests : IDisposable
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"sortus_flow_{Guid.NewGuid():N}.db");

    public MvpFlowTests()
    {
        Database.OverrideDbPath = _dbPath;
    }

    public void Dispose()
    {
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
        }
    }

    private sealed class PassThroughLocalization : Core.Services.ILocalizationService
    {
        public string CurrentCulture => "zh-CN";

        public event EventHandler? CultureChanged
        {
            add { }
            remove { }
        }

        public void Initialize()
        {
        }

        public void SetCulture(string cultureName)
        {
        }

        public string GetString(string key) => key;
    }

    private sealed class AlwaysDiscardDialog : IDialogService
    {
        public event Action<object?>? ActiveDialogChanged
        {
            add { }
            remove { }
        }

        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(true);

        /// <summary>破坏性确认（VD-117）：测试里一律放行，模拟用户点了确认。</summary>
        public Task<bool> ConfirmDestructiveAsync(string title, string message) => Task.FromResult(true);

        public Task<UnsavedChangesChoice> ConfirmUnsavedAsync(string title, string message) =>
            Task.FromResult(UnsavedChangesChoice.Discard);

        public Task InfoAsync(string title, string message) => Task.CompletedTask;

        // 端到端用例不涉及覆盖层对话框：直接视为用户点关闭。
        public Task ShowAsync(IOverlayDialogViewModel viewModel)
        {
            viewModel.RequestClose();
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task FullLoop_CreateFromWizard_EditInDetail_SurvivesRestart()
    {
        await MigrationRunner.RunAsync();

        var localization = new PassThroughLocalization();
        var service = new ProjectService(new ProjectRepository());

        // 1) 向导创建立项
        var wizard = new ProjectWizardViewModel(service, localization, () => { });
        wizard.SelectTypeCommand.Execute(ProjectType.Programming);
        wizard.NextStepCommand.Execute(null);
        wizard.Name = "端到端项目";
        wizard.Summary = "验证整条链路";
        wizard.SubType = "桌面应用";
        wizard.FolderPath = @"D:\code\e2e";
        wizard.ScheduleStart = new DateTime(2026, 2, 1);
        wizard.ScheduleEnd = new DateTime(2026, 4, 1);
        wizard.TogglePresetCommand.Execute(wizard.TechStackPresets.First(p => p.Name == "WPF"));
        wizard.TogglePresetCommand.Execute(wizard.TechStackPresets.First(p => p.Name == "C#"));

        await ((Common.AsyncRelayCommand)wizard.CreateCommand).ExecuteAsync();
        Assert.Empty(wizard.ErrorMessage);

        // 2) 列表可见（同一服务实例，模拟界面刷新）
        var list = new ProjectListViewModel(service, localization, _ => { }, () => { });
        await list.LoadAsync();
        var listed = Assert.Single(list.Projects);
        Assert.Equal("端到端项目", listed.Name);
        var createdId = listed.Id;

        // 3) 详情页编辑过程字段与进度
        var detail = new ProjectDetailViewModel(service, localization, new AlwaysDiscardDialog(), listed, () => { });
        detail.Project.DevPlan = "里程碑一、里程碑二";
        detail.Project.AcceptanceCriteria = "数据重启不丢";
        detail.Status = ProjectStatus.Active;
        detail.Project.ProgressPercent = 37.5;
        await ((Common.AsyncRelayCommand)detail.SaveCommand).ExecuteAsync();
        Assert.False(detail.IsDirty);

        // 4) 模拟重启：全新仓储/服务实例读同一数据库文件
        var restartedService = new ProjectService(new ProjectRepository());
        var reloaded = await restartedService.GetByIdAsync(createdId);

        Assert.NotNull(reloaded);
        Assert.Equal("端到端项目", reloaded!.Name);
        Assert.Equal("里程碑一、里程碑二", reloaded.DevPlan);
        Assert.Equal("数据重启不丢", reloaded.AcceptanceCriteria);
        Assert.Equal(ProjectStatus.Active, reloaded.Status);
        Assert.Equal(37.5, reloaded.ProgressPercent);
        Assert.Equal("WPF;C#", reloaded.TechStack);
        Assert.Equal(new DateTime(2026, 2, 1), reloaded.ScheduleStart);

        // 5) 筛选：状态/关键字命中，错误条件不命中
        var active = await restartedService.QueryAsync(null, ProjectStatus.Active, null);
        Assert.Single(active);
        var byKeyword = await restartedService.QueryAsync(null, null, "端到端");
        Assert.Single(byKeyword);
        var miss = await restartedService.QueryAsync(ProjectType.Clip, null, null);
        Assert.Empty(miss);
    }

    [Fact]
    public async Task Delete_RemovesProject_AndListBecomesEmpty()
    {
        await MigrationRunner.RunAsync();

        var localization = new PassThroughLocalization();
        var service = new ProjectService(new ProjectRepository());

        var wizard = new ProjectWizardViewModel(service, localization, () => { });
        wizard.Name = "待删除";
        wizard.TogglePresetCommand.Execute(wizard.TechStackPresets.First(p => p.Name == "Go"));
        await ((Common.AsyncRelayCommand)wizard.CreateCommand).ExecuteAsync();

        var listed = (await service.GetAllAsync()).Single();
        var detail = new ProjectDetailViewModel(service, localization, new AlwaysDiscardDialog(), listed, () => { });
        await ((Common.AsyncRelayCommand)detail.DeleteCommand).ExecuteAsync();

        Assert.Empty(await service.GetAllAsync());
    }
}
