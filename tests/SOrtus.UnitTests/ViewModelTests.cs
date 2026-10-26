using System.Globalization;
using System.Linq;
using SOrtus.Core.Models;
using SOrtus.Navigation;
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

        /// <summary>最近一次查询收到的类型筛选（null = 不筛选）。</summary>
        public ProjectType? LastType { get; private set; }

        /// <summary>最近一次查询收到的状态筛选（null = 不筛选）。</summary>
        public ProjectStatus? LastStatus { get; private set; }

        public Task<IReadOnlyList<Project>> GetAllAsync() =>
            Task.FromResult<IReadOnlyList<Project>>(Items.ToList());

        public Task<IReadOnlyList<Project>> QueryAsync(ProjectType? type, ProjectStatus? status, string? keyword)
        {
            LastType = type;
            LastStatus = status;
            return Task.FromResult<IReadOnlyList<Project>>(Items.ToList());
        }

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

        /// <summary>破坏性确认（VD-117）走同一个桩结果，便于用例既覆盖普通确认也覆盖删除确认。</summary>
        public Task<bool> ConfirmDestructiveAsync(string title, string message) => Task.FromResult(ConfirmResult);

        public Task<UnsavedChangesChoice> ConfirmUnsavedAsync(string title, string message)
        {
            UnsavedPrompts++;
            return Task.FromResult(UnsavedChoice);
        }

        public Task InfoAsync(string title, string message) => Task.CompletedTask;

        /// <summary>被展示过的覆盖层对话框（供断言是否弹出）。</summary>
        public List<IOverlayDialogViewModel> ShownDialogs { get; } = [];

        /// <summary>展示的覆盖层对话框立即视为关闭，避免用例悬挂。</summary>
        public Task ShowAsync(IOverlayDialogViewModel viewModel)
        {
            ShownDialogs.Add(viewModel);
            viewModel.RequestClose();
            return Task.CompletedTask;
        }
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

        // VD-008：校验错误就近落在字段级属性；VD-091：页脚另给一句汇总（不再静默 return）
        await ((Common.AsyncRelayCommand)vm.CreateCommand).ExecuteAsync();
        Assert.Equal("Common_RequiredName", vm.NameError);
        Assert.True(vm.HasNameError);
        Assert.NotEmpty(vm.ErrorMessage);

        vm.Name = "新项目";
        await ((Common.AsyncRelayCommand)vm.CreateCommand).ExecuteAsync();
        Assert.Equal("Common_RequiredTechStack", vm.TechStackError);
        Assert.True(vm.HasTechStackError);
        Assert.Null(vm.NameError);
        Assert.NotEmpty(vm.ErrorMessage);
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

        // VD-008：剪辑类型错误就近落在 ClipTypeError
        await ((Common.AsyncRelayCommand)vm.CreateCommand).ExecuteAsync();
        Assert.Equal("Common_RequiredClipType", vm.ClipTypeError);
        Assert.True(vm.HasClipTypeError);
        Assert.NotEmpty(vm.ErrorMessage);

        vm.SubType = vm.SubTypeOptions.First();
        await ((Common.AsyncRelayCommand)vm.CreateCommand).ExecuteAsync();

        Assert.Null(vm.ClipTypeError);
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

        // VD-008：待办完成时间错误就近落在 DueTimeError
        await ((Common.AsyncRelayCommand)vm.CreateCommand).ExecuteAsync();
        Assert.Equal("Common_RequiredDueTime", vm.DueTimeError);
        Assert.True(vm.HasDueTimeError);
        Assert.NotEmpty(vm.ErrorMessage);
        Assert.Empty(service.Items);

        vm.ScheduleEnd = new DateTime(2026, 12, 1);
        await ((Common.AsyncRelayCommand)vm.CreateCommand).ExecuteAsync();

        Assert.Null(vm.DueTimeError);
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

    #region 列表页 / 导航（VD-079 保留筛选与滚动、VD-087 清除筛选）

    /// <summary>主题服务桩：只要满足构造签名，不触碰磁盘。</summary>
    private sealed class StubThemeService : IThemeService
    {
        public string CurrentTheme { get; private set; } = "Light";

        public double GlobalBlurRadius { get; private set; } = 12;

        public event EventHandler? ThemeChanged
        {
            add { }
            remove { }
        }

        public void Initialize()
        {
        }

        public void SetTheme(string themeName) => CurrentTheme = themeName;

        public void SetBlurStrength(double radius) => GlobalBlurRadius = radius;

        /// <summary>拖动滑块时的临时预览（VD-109）：桩只记录最近一次值，不落盘。</summary>
        public void PreviewBlurStrength(double radius) => GlobalBlurRadius = radius;

        /// <summary>松手提交（VD-109）：桩无需额外动作。</summary>
        public void CommitBlurStrength()
        {
        }
    }

    [Fact]
    public async Task List_ClearFilters_ResetsConditionsAndReloads()
    {
        var service = new StubProjectService();
        service.Items.Add(new Project { Id = 1, Name = "A", Type = ProjectType.Programming });
        var vm = new ProjectListViewModel(service, new StubLocalization(), _ => { }, () => { });
        await vm.LoadAsync();

        vm.SearchText = "abc";
        vm.FilterType = vm.TypeOptions.Single(o => o.Value is ProjectType.Programming);
        vm.FilterStatus = vm.StatusOptions.Single(o => o.Value is ProjectStatus.Completed);
        Assert.True(vm.HasFilter);

        vm.ClearFiltersCommand.Execute(null);

        // 清空是同步的（先复位条件再统一刷新一次），因此无需等待 LoadAsync
        Assert.Equal(string.Empty, vm.SearchText);
        // 复位到「全部」真实项（不再是 null——null 项在 ComboBox 里选不回来）
        Assert.True(vm.FilterType.IsAll);
        Assert.True(vm.FilterStatus.IsAll);
        Assert.False(vm.HasFilter);
    }

    [Fact]
    public async Task List_Filters_DefaultToAllOption_AndAllIsSelectableBack()
    {
        var service = new StubProjectService();
        var vm = new ProjectListViewModel(service, new StubLocalization(), _ => { }, () => { });
        await vm.LoadAsync();

        // 默认选中「全部」真实项，不是「无选中」；查询层收到 null（= 不筛选）
        Assert.NotNull(vm.FilterType);
        Assert.True(vm.FilterType.IsAll);
        Assert.NotNull(vm.FilterStatus);
        Assert.True(vm.FilterStatus.IsAll);
        Assert.False(vm.HasFilter);
        Assert.Null(service.LastType);
        Assert.Null(service.LastStatus);

        // 「全部」与普通项同链路：选得出（值真的下推到查询层），也选得回（用户 2026-10-25 反馈的根因）
        vm.FilterType = vm.TypeOptions.Single(o => o.Value is ProjectType.Clip);
        await vm.LoadAsync();
        Assert.True(vm.HasFilter);
        Assert.Equal(ProjectType.Clip, service.LastType);

        vm.FilterType = vm.TypeOptions[0];
        await vm.LoadAsync();
        Assert.False(vm.HasFilter);
        Assert.Null(service.LastType);
    }

    [Fact]
    public async Task Main_ReturningToList_KeepsSameListViewModel()
    {
        var navigation = new NavigationService();
        var vm = new MainViewModel(
            new StubProjectService(),
            new StubThemeService(),
            new StubLocalization(),
            navigation,
            new StubDialogService());

        vm.Initialize();
        var listBefore = vm.CurrentView;

        await vm.ShowNewProjectAsync();
        Assert.NotSame(listBefore, vm.CurrentView);

        await vm.ShowProjectsAsync();
        Assert.Same(listBefore, vm.CurrentView);
    }

    #endregion

    #region VD-085 / VD-090 / VD-091 / VD-092（本轮新增行为）

    [Fact]
    public async Task Wizard_ConfirmLeave_OnlyAsksWhenDirty()
    {
        var dialog = new StubDialogService { ConfirmResult = false };
        var vm = new ProjectWizardViewModel(new StubProjectService(), new StubLocalization(), () => { }, dialog);

        // 没有任何输入：直接放行，不打扰用户
        Assert.False(vm.IsDirty);
        Assert.True(await vm.ConfirmLeaveAsync());

        // 填了名字：必须问一次；用户选「否」则不允许离开
        vm.Name = "半成品";
        Assert.True(vm.IsDirty);
        Assert.False(await vm.ConfirmLeaveAsync());

        dialog.ConfirmResult = true;
        Assert.True(await vm.ConfirmLeaveAsync());
    }

    [Fact]
    public async Task Wizard_Validation_RequestsFocusOnFirstErrorField()
    {
        var vm = new ProjectWizardViewModel(new StubProjectService(), new StubLocalization(), () => { });
        string? requested = null;
        vm.FocusRequested += anchor => requested = anchor;

        await ((Common.AsyncRelayCommand)vm.CreateCommand).ExecuteAsync();
        Assert.Equal("Name", requested);

        // 名称补上后，第一个错误变成技术栈
        requested = null;
        vm.Name = "有名字没技术栈";
        await ((Common.AsyncRelayCommand)vm.CreateCommand).ExecuteAsync();
        Assert.Equal("TechStack", requested);
    }

    [Fact]
    public void List_ClearSearchCommand_ClearsKeyword()
    {
        var vm = new ProjectListViewModel(new StubProjectService(), new StubLocalization(), _ => { }, () => { });
        vm.SearchText = "剪辑";
        Assert.True(vm.IsSearchActive);

        vm.ClearSearchCommand.Execute(null);

        Assert.Equal(string.Empty, vm.SearchText);
        Assert.False(vm.IsSearchActive);
    }

    [Fact]
    public void UpdatedAtConverter_ShowsYearOnlyWhenCrossYear()
    {
        var converter = new Converters.UpdatedAtDisplayConverter();

        var sameYear = converter.Convert(new DateTime(DateTime.Now.Year, 3, 4), typeof(string), null, CultureInfo.InvariantCulture);
        var lastYear = converter.Convert(new DateTime(DateTime.Now.Year - 1, 3, 4), typeof(string), null, CultureInfo.InvariantCulture);

        // 无资源宿主时回落到内置格式串，关键差异是「跨年必须带年份」
        Assert.DoesNotContain((DateTime.Now.Year - 1).ToString(), sameYear!.ToString()!.Replace($"{DateTime.Now.Year - 1}", ""));
        Assert.Contains((DateTime.Now.Year - 1).ToString(), lastYear!.ToString());
    }

    [Fact]
    public async Task List_SearchDebounce_CollapsesRapidKeystrokes()
    {
        var service = new CountingProjectService();
        var vm = new ProjectListViewModel(service, new StubLocalization(), _ => { }, () => { });
        await vm.LoadAsync();
        var baseline = service.QueryCount;

        // 模拟输入法组字：连续三次 PropertyChanged 应只触发一次查询（VD-086）
        vm.SearchText = "剪";
        vm.SearchText = "剪辑";
        vm.SearchText = "剪辑项";

        await Task.Delay(ProjectListViewModel.SearchDebounceMs + 250);

        Assert.Equal(baseline + 1, service.QueryCount);
    }

    [Fact]
    public void AddRemoteDialog_EscNeedsTwoPressesWhenDirty()
    {
        var vm = new AddRemoteDialogViewModel(
            new SmokeGitServiceForDialog(),
            ".",
            "origin",
            "https://example.com/repo.git",
            new StubLocalization());

        // 第一次 Esc：拦下并给出提示
        Assert.False(vm.ConfirmEsc());
        Assert.True(vm.ShowEscHint);

        // 第二次 Esc：真正放行
        Assert.True(vm.ConfirmEsc());
    }

    /// <summary>统计查询次数的最小桩（VD-086）。</summary>
    private sealed class CountingProjectService : IProjectService
    {
        public int QueryCount { get; private set; }

        public Task<IReadOnlyList<Project>> GetAllAsync() =>
            Task.FromResult<IReadOnlyList<Project>>(new List<Project>());

        public Task<IReadOnlyList<Project>> QueryAsync(ProjectType? type, ProjectStatus? status, string? keyword)
        {
            QueryCount++;
            return Task.FromResult<IReadOnlyList<Project>>(new List<Project>());
        }

        public Task<Project?> GetByIdAsync(int id) => Task.FromResult<Project?>(null);

        public Task<Project> CreateAsync(Project project) => Task.FromResult(project);

        public Task<Project> SaveAsync(Project project) => Task.FromResult(project);

        public Task DeleteAsync(int id) => Task.CompletedTask;
    }

    /// <summary>配置远端弹窗只需要一个满足签名的 Git 桩。</summary>
    private sealed class SmokeGitServiceForDialog : IGitService
    {
        public Task<GitRepositoryInfo> InspectAsync(string folderPath, int commitCount = 30) =>
            Task.FromResult(new GitRepositoryInfo(false, folderPath, string.Empty, false, false, 0, 0,
                new List<GitBranchInfo>(), new List<GitCommitInfo>(), new List<GitRemoteInfo>()));

        public Task<GitOperationResult> AddOrSetRemoteAsync(string folderPath, string name, string url) =>
            Task.FromResult(GitOperationResult.Ok());

        public Task<GitOperationResult> PushAsync(string folderPath, string branch, string remote, bool setUpstream) =>
            Task.FromResult(GitOperationResult.Ok());
    }

    #endregion

    #region 第三批（VD-101～120）

    [Fact]
    public async Task Git_Timeline_ShowsTenAtFirst_ThenGrowsAndStops()
    {
        var vm = new GitViewModel(
            new FixedCommitsGitService(25),
            new StubLocalization(),
            new StubDialogService(),
            new Project { Id = 1, Name = "P", FolderPath = @"D:\repo" });

        await vm.ActivateAsync();

        // VD-107：一进版块只放 10 条，避免 50 条提交一次铺满整页
        Assert.Equal(10, vm.Timeline.Count);
        Assert.True(vm.HasMoreCommits);

        vm.ShowMoreCommitsCommand.Execute(null);
        Assert.Equal(20, vm.Timeline.Count);

        vm.ShowMoreCommitsCommand.Execute(null);
        Assert.Equal(25, vm.Timeline.Count);
        Assert.False(vm.HasMoreCommits);

        // 没有更多了就必须禁用命令，否则按钮可点但毫无反应
        Assert.False(vm.ShowMoreCommitsCommand.CanExecute(null));
    }

    [Fact]
    public async Task Git_Timeline_UnderTen_ShowsNoMoreButton()
    {
        var vm = new GitViewModel(
            new FixedCommitsGitService(4),
            new StubLocalization(),
            new StubDialogService(),
            new Project { Id = 1, Name = "P", FolderPath = @"D:\repo" });

        await vm.ActivateAsync();

        Assert.Equal(4, vm.Timeline.Count);
        Assert.False(vm.HasMoreCommits);
    }

    [Fact]
    public void ConfirmDialog_Destructive_IsFlaggedForDangerStyling()
    {
        // VD-117：破坏性确认必须带上标记，界面据此用实心危险按钮 + 标题警示图标，
        // 且默认焦点不在确认键上（ISDestructive 为 true 时不设 IsDefault）。
        var destructive = new ConfirmDialogViewModel("删除立项", "不可撤销", isConfirm: true, new StubLocalization(), isDestructive: true);
        var normal = new ConfirmDialogViewModel("重命名", "确认吗？", isConfirm: true, new StubLocalization());

        Assert.True(destructive.IsDestructive);
        Assert.True(destructive.IsConfirm);
        Assert.False(normal.IsDestructive);
    }

    [Fact]
    public void AppSettings_NullCulture_FallsBackToDefault()
    {
        // 回归：设置页语言下拉在选中项为 null 时会把 settings.json 写成 "Culture": null，
        // 下次启动 new CultureInfo(null) 直接抛 ArgumentNullException——应用起不来。
        var settings = new SOrtus.Services.AppSettings { Culture = null! };
        Assert.Equal("zh-CN", settings.Culture);

        settings.Culture = string.Empty;
        Assert.Equal("zh-CN", settings.Culture);

        settings.Culture = "en-US";
        Assert.Equal("en-US", settings.Culture);
    }

    [Fact]
    public void FocusRingSettings_TogglesWithoutApplicationInstance()
    {
        // 单测进程没有 Application.Current：开关仍要能记录状态且不抛
        FocusRingSettings.Apply(false);
        Assert.False(FocusRingSettings.IsEnabled);

        FocusRingSettings.Apply(true);
        Assert.True(FocusRingSettings.IsEnabled);
    }

    /// <summary>固定条数的 Git 桩（时间线分页用）。</summary>
    private sealed class FixedCommitsGitService : IGitService
    {
        private readonly int _count;

        public FixedCommitsGitService(int count) => _count = count;

        public Task<GitRepositoryInfo> InspectAsync(string folderPath, int commitCount = 30)
        {
            var commits = Enumerable.Range(0, _count)
                .Select(i => new GitCommitInfo(
                    $"sha{i:D4}",
                    $"sha{i:D4}",
                    "Song",
                    DateTimeOffset.Now.AddMinutes(-i),
                    $"feat: 第 {i} 条提交",
                    Array.Empty<string>()))
                .ToList();

            var branches = new List<GitBranchInfo>
            {
                new("master", true, false, commits[0].Sha, commits[0].Subject, commits[0].When, 0, 0),
            };

            return Task.FromResult(new GitRepositoryInfo(
                true, folderPath, "master", true, true, 0, 0, branches, commits, new List<GitRemoteInfo>()));
        }

        public Task<GitOperationResult> AddOrSetRemoteAsync(string folderPath, string name, string url) =>
            Task.FromResult(GitOperationResult.Ok());

        public Task<GitOperationResult> PushAsync(string folderPath, string branch, string remote, bool setUpstream) =>
            Task.FromResult(GitOperationResult.Ok());
    }

    #endregion
}
