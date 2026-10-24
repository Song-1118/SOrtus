using SOrtus.Core.Models;
using SOrtus.Core.Services;
using SOrtus.Services;
using SOrtus.ViewModels;
using Xunit;

namespace SOrtus.UnitTests;

/// <summary>
/// 视图模型单元测试（验收 A5 / A6 / A8 / A12）：不依赖窗口与真实数据库，用桩服务驱动。
/// </summary>
public class ViewModelTests
{
    #region 桩实现

    private sealed class StubLocalization : ILocalizationService
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

    private sealed class StubProjectService : IProjectService
    {
        public List<Project> Items { get; } = new();

        public int SaveCalls { get; private set; }

        public Task<IReadOnlyList<Project>> GetAllAsync() =>
            Task.FromResult<IReadOnlyList<Project>>(Items.ToList());

        public Task<IReadOnlyList<Project>> QueryAsync(ProjectType? type, ProjectStatus? status, string? keyword) =>
            Task.FromResult<IReadOnlyList<Project>>(Items.ToList());

        public Task<Project?> GetByIdAsync(int id) =>
            Task.FromResult(Items.FirstOrDefault(p => p.Id == id));

        public Task<Project> CreateAsync(Project project)
        {
            project.Id = Items.Count + 1;
            project.CreatedAt = DateTime.UtcNow;
            project.UpdatedAt = project.CreatedAt;
            Items.Add(project);
            return Task.FromResult(project);
        }

        public Task<Project> SaveAsync(Project project)
        {
            SaveCalls++;
            project.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult(project);
        }

        public Task DeleteAsync(int id)
        {
            Items.RemoveAll(p => p.Id == id);
            return Task.CompletedTask;
        }
    }

    private sealed class StubDialogService : IDialogService
    {
        public bool ConfirmResult { get; set; }

        public UnsavedChangesChoice UnsavedChoice { get; set; } = UnsavedChangesChoice.Cancel;

        public int UnsavedPrompts { get; private set; }

        public event Action<object?>? ActiveDialogChanged
        {
            add { }
            remove { }
        }

        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(ConfirmResult);

        public Task<UnsavedChangesChoice> ConfirmUnsavedAsync(string title, string message)
        {
            UnsavedPrompts++;
            return Task.FromResult(UnsavedChoice);
        }

        public Task InfoAsync(string title, string message) => Task.CompletedTask;
    }

    #endregion

    #region 向导（A5 / A6）

    [Fact]
    public void Wizard_PresetToggle_SyncsWithTechStackList()
    {
        var vm = new ProjectWizardViewModel(new StubProjectService(), new StubLocalization(), () => { });
        var preset = vm.TechStackPresets.First(p => p.Name == "WPF");

        vm.TogglePresetCommand.Execute(preset);
        Assert.True(preset.IsSelected);
        Assert.Contains("WPF", vm.TechStacks);

        vm.TogglePresetCommand.Execute(preset);
        Assert.False(preset.IsSelected);
        Assert.DoesNotContain("WPF", vm.TechStacks);
    }

    [Fact]
    public void Wizard_CustomInput_SupportsMultipleValues_AndLightsUpMatchingPreset()
    {
        var vm = new ProjectWizardViewModel(new StubProjectService(), new StubLocalization(), () => { });

        vm.TechStackInput = "Rust, TypeScript；自研引擎";
        vm.AddTechStackCommand.Execute(null);

        Assert.Equal(new[] { "Rust", "TypeScript", "自研引擎" }, vm.TechStacks);
        Assert.True(vm.TechStackPresets.First(p => p.Name == "Rust").IsSelected);
        Assert.Empty(vm.TechStackInput);
    }

    [Fact]
    public void Wizard_RemoveChip_ClearsPresetSelection()
    {
        var vm = new ProjectWizardViewModel(new StubProjectService(), new StubLocalization(), () => { });
        var preset = vm.TechStackPresets.First(p => p.Name == "C#");
        vm.TogglePresetCommand.Execute(preset);

        vm.RemoveTechStackCommand.Execute("C#");

        Assert.Empty(vm.TechStacks);
        Assert.False(preset.IsSelected);
    }

    [Fact]
    public async Task Wizard_Create_RequiresNameAndTechStack()
    {
        var service = new StubProjectService();
        var created = 0;
        var vm = new ProjectWizardViewModel(service, new StubLocalization(), () => created++);

        await ((Common.AsyncRelayCommand)vm.CreateCommand).ExecuteAsync();
        Assert.Equal("Common_RequiredName", vm.ErrorMessage);

        vm.Name = "新项目";
        await ((Common.AsyncRelayCommand)vm.CreateCommand).ExecuteAsync();
        Assert.Equal("Common_RequiredTechStack", vm.ErrorMessage);
        Assert.Empty(service.Items);
        Assert.Equal(0, created);
    }

    [Fact]
    public async Task Wizard_Create_SavesAllCollectedFields()
    {
        var service = new StubProjectService();
        var created = 0;
        var vm = new ProjectWizardViewModel(service, new StubLocalization(), () => created++);

        vm.SelectTypeCommand.Execute(ProjectType.Programming);
        vm.NextStepCommand.Execute(null);
        vm.Name = "  新项目  ";
        vm.Summary = "简介";
        vm.SubType = "桌面应用";
        vm.FolderPath = @"D:\code\demo";
        vm.ScheduleStart = new DateTime(2026, 1, 1);
        vm.ScheduleEnd = new DateTime(2026, 3, 1);
        vm.TogglePresetCommand.Execute(vm.TechStackPresets.First(p => p.Name == "WPF"));

        await ((Common.AsyncRelayCommand)vm.CreateCommand).ExecuteAsync();

        var project = Assert.Single(service.Items);
        Assert.Equal("新项目", project.Name);
        Assert.Equal(ProjectType.Programming, project.Type);
        Assert.Equal("简介", project.Summary);
        Assert.Equal("桌面应用", project.SubType);
        Assert.Equal(@"D:\code\demo", project.FolderPath);
        Assert.Equal("WPF", project.TechStack);
        Assert.Equal(new DateTime(2026, 1, 1), project.ScheduleStart);
        Assert.Equal(new DateTime(2026, 3, 1), project.ScheduleEnd);
        Assert.Equal(1, created);
    }

    [Fact]
    public async Task Wizard_CreateClip_RequiresClipType_NotTechStack()
    {
        // SO-002 回归：剪辑类型不校验技术栈（界面上没有该字段），但要求选剪辑类型
        var service = new StubProjectService();
        var vm = new ProjectWizardViewModel(service, new StubLocalization(), () => { });
        vm.SelectTypeCommand.Execute(ProjectType.Clip);
        vm.NextStepCommand.Execute(null);
        vm.Name = "剪辑测试";

        await ((Common.AsyncRelayCommand)vm.CreateCommand).ExecuteAsync();
        Assert.Equal("Common_RequiredClipType", vm.ErrorMessage);

        vm.SubType = vm.SubTypeOptions.First();
        await ((Common.AsyncRelayCommand)vm.CreateCommand).ExecuteAsync();

        Assert.Empty(vm.ErrorMessage);
        var project = Assert.Single(service.Items);
        Assert.Equal(ProjectType.Clip, project.Type);
        Assert.Equal("剪辑测试", project.Name);
    }

    [Fact]
    public async Task Wizard_CreateTodo_RequiresDueDate()
    {
        // SO-002 + SO-016 回归：待办不校验技术栈，但完成时间必填
        var service = new StubProjectService();
        var vm = new ProjectWizardViewModel(service, new StubLocalization(), () => { });
        vm.SelectTypeCommand.Execute(ProjectType.Todo);
        vm.NextStepCommand.Execute(null);
        vm.Name = "待办测试";
        vm.ScheduleEnd = null;

        await ((Common.AsyncRelayCommand)vm.CreateCommand).ExecuteAsync();
        Assert.Equal("Common_RequiredDueTime", vm.ErrorMessage);
        Assert.Empty(service.Items);

        vm.ScheduleEnd = new DateTime(2026, 12, 1);
        await ((Common.AsyncRelayCommand)vm.CreateCommand).ExecuteAsync();

        Assert.Empty(vm.ErrorMessage);
        Assert.Equal(ProjectType.Todo, Assert.Single(service.Items).Type);
    }

    [Fact]
    public void Wizard_SwitchingType_ResetsSubType()
    {
        var vm = new ProjectWizardViewModel(new StubProjectService(), new StubLocalization(), () => { });
        vm.SelectTypeCommand.Execute(ProjectType.Programming);
        vm.SubType = "桌面应用";
        vm.SelectTypeCommand.Execute(ProjectType.Clip);

        Assert.Empty(vm.SubType);
        Assert.True(vm.IsClip);
        Assert.False(vm.IsProgramming);
    }

    #endregion

    #region 详情（A8 / A12）

    private static ProjectDetailViewModel CreateDetail(
        out StubProjectService service,
        out StubDialogService dialog,
        Project? project = null)
    {
        service = new StubProjectService();
        dialog = new StubDialogService();
        var entity = project ?? new Project { Id = 1, Name = "示例", Type = ProjectType.Programming };
        return new ProjectDetailViewModel(service, new StubLocalization(), dialog, entity, () => { });
    }

    [Fact]
    public void Detail_StartsClean_AndBecomesDirtyOnEdit()
    {
        var vm = CreateDetail(out _, out _);
        Assert.False(vm.IsDirty);

        vm.TechStackText = "WPF;SQLite";
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task Detail_TryLeave_AsksUserOnlyWhenDirty()
    {
        var vm = CreateDetail(out _, out var dialog);

        Assert.True(await vm.TryLeaveAsync());
        Assert.Equal(0, dialog.UnsavedPrompts);

        vm.TechStackText = "WPF";
        dialog.UnsavedChoice = UnsavedChangesChoice.Cancel;
        Assert.False(await vm.TryLeaveAsync());
        Assert.Equal(1, dialog.UnsavedPrompts);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task Detail_TryLeave_Discard_LeavesWithoutSaving()
    {
        var vm = CreateDetail(out var service, out var dialog);
        vm.TechStackText = "WPF";
        dialog.UnsavedChoice = UnsavedChangesChoice.Discard;

        Assert.True(await vm.TryLeaveAsync());
        Assert.Equal(0, service.SaveCalls);
    }

    [Fact]
    public async Task Detail_TryLeave_Save_PersistsAndClearsDirty()
    {
        var vm = CreateDetail(out var service, out var dialog);
        vm.Project.Goals = "完成 MVP";
        dialog.UnsavedChoice = UnsavedChangesChoice.Save;

        Assert.True(await vm.TryLeaveAsync());
        Assert.Equal(1, service.SaveCalls);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task Detail_StatusChange_IsDirtyAndPersisted()
    {
        var vm = CreateDetail(out var service, out _);
        Assert.Equal(ProjectStatus.Planning, vm.Status);

        vm.Status = ProjectStatus.Completed;
        Assert.True(vm.IsDirty);

        await ((Common.AsyncRelayCommand)vm.SaveCommand).ExecuteAsync();
        Assert.Equal(ProjectStatus.Completed, vm.Project.Status);
        Assert.Equal(1, service.SaveCalls);
        Assert.False(vm.IsDirty);
    }

    #endregion
}
