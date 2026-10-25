using System.Collections.ObjectModel;
using System.Windows.Input;
using SOrtus.Common;
using SOrtus.Core.Models;
using SOrtus.Core.Repositories;
using SOrtus.Core.Services;
using SOrtus.Navigation;
using SOrtus.Services;

namespace SOrtus.ViewModels;

/// <summary>
/// 主视图模型：承载导航与内容区切换（规范：简单内容区切换，不引入重型框架）。
/// 内容区通过 ViewTemplates.xaml 将视图模型映射到对应 XAML 视图。
/// 同时承载主窗口侧边栏导航高亮、对话框覆盖层状态，以及「离开详情页前先处理未保存更改」的守卫（A12）。
/// </summary>
internal sealed class MainViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IThemeService _themeService;
    private readonly ILocalizationService _localization;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogService;
    private readonly IProjectDocumentRepository? _documentRepository;
    private readonly IDocumentImportService? _importService;
    private readonly IGitService? _gitService;
    private readonly IFilePickerService? _filePicker;

    private readonly AsyncRelayCommand _navigateProjectsCommand;
    private readonly AsyncRelayCommand _navigateNewCommand;
    private readonly AsyncRelayCommand _navigateSettingsCommand;
    private readonly RelayCommand _closeDialogCommand;

    private object? _dialogContent;
    private ProjectListViewModel? _listViewModel;
    private int _dialogCloseVersion;

    public ViewModelBase CurrentView => _navigation.Current;

    /// <summary>当前导航标识（"Projects" / "New" / "Settings"），供侧边栏高亮。</summary>
    public string ActiveNav => _navigation.Current switch
    {
        ProjectListViewModel => "Projects",
        ProjectWizardViewModel => "New",
        SettingsViewModel => "Settings",
        ProjectDetailViewModel => "Projects",
        _ => "Projects",
    };

    /// <summary>当前页面是否有未保存的更改（供关窗拦截同步判断）。</summary>
    public bool HasUnsavedChanges => _navigation.Current is ProjectDetailViewModel { IsDirty: true };

    /// <summary>对话框覆盖层内容（null = 关闭）。</summary>
    public object? DialogContent
    {
        get => _dialogContent;
        private set => SetProperty(ref _dialogContent, value);
    }

    public bool IsDialogOpen => _dialogContent is not null;

    /// <summary>
    /// 对话框正在播放退场动画（VD-063）。
    /// 关闭时先置位让覆盖层 140ms 淡出，动画结束后再卸载内容；
    /// 否则覆盖层会「啪」地消失，与入场弹入的节奏不对称。
    /// </summary>
    public bool IsDialogClosing
    {
        get => _isDialogClosing;
        private set => SetProperty(ref _isDialogClosing, value);
    }

    private bool _isDialogClosing;

    public MainViewModel(
        IProjectService projectService,
        IThemeService themeService,
        ILocalizationService localization,
        INavigationService navigation,
        IDialogService dialogService,
        IProjectDocumentRepository? documentRepository = null,
        IDocumentImportService? importService = null,
        IGitService? gitService = null,
        IFilePickerService? filePicker = null)
    {
        _projectService = projectService;
        _themeService = themeService;
        _localization = localization;
        _navigation = navigation;
        _dialogService = dialogService;
        _documentRepository = documentRepository;
        _importService = importService;
        _gitService = gitService;
        _filePicker = filePicker;
        _navigation.Navigated += (_, _) =>
        {
            OnPropertyChanged(nameof(CurrentView));
            OnPropertyChanged(nameof(ActiveNav));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            _ = RefreshRecentAsync();
        };
        _dialogService.ActiveDialogChanged += OnActiveDialogChanged;

        _navigateProjectsCommand = new AsyncRelayCommand(() => ShowProjectsAsync());
        _navigateNewCommand = new AsyncRelayCommand(() => ShowNewProjectAsync());
        _navigateSettingsCommand = new AsyncRelayCommand(() => ShowSettingsAsync());
        _closeDialogCommand = new RelayCommand(_ => CloseDialog(isScrimClick: false));
        ScrimCloseCommand = new RelayCommand(_ => CloseDialog(isScrimClick: true));
        OpenRecentCommand = new AsyncRelayCommand(OpenRecentAsync);

        Updates = new UpdatesViewModel(dialogService, localization);

        // VD-110：语言切换只重建了 XAML 侧字符串字典，视图模型里拼好的文案不会自己刷新。
        // 主视图模型在进程内只有一个实例，由它统一转发给「当前页」与常驻复用的列表页，
        // 避免每个页面各自订阅（导航一次泄漏一次）。
        _localization.CultureChanged += (_, _) => RefreshLocalizedText();
    }

    /// <summary>把语言变更转发给需要重算文案的视图模型（VD-110）。</summary>
    private void RefreshLocalizedText()
    {
        OnCultureChanged();
        _listViewModel?.OnCultureChanged();
        CurrentView.OnCultureChanged();
    }

    /// <summary>
    /// 更新编排（顶部提示条 + 手动检查入口）。
    /// 由 App 在窗口显示后延迟触发一次自动检查；本对象自身不主动联网。
    /// </summary>
    public UpdatesViewModel Updates { get; }

    public ICommand NavigateProjectsCommand => _navigateProjectsCommand;
    public ICommand NavigateNewCommand => _navigateNewCommand;
    public ICommand NavigateSettingsCommand => _navigateSettingsCommand;

    /// <summary>关闭对话框（遮罩点击 / Esc，等同取消）。</summary>
    public ICommand CloseDialogCommand => _closeDialogCommand;

    /// <summary>
    /// 点击遮罩关闭对话框（VD-083）。与 Esc 分开：
    /// 有输入的配置类弹窗（<see cref="IOverlayDialogViewModel.AllowScrimClose"/> = false）点遮罩不响应。
    /// </summary>
    public ICommand ScrimCloseCommand { get; }

    /// <summary>侧边栏「最近立项」（VD-082：导航区下方不再是一整片空白）。</summary>
    public ObservableCollection<Project> RecentProjects { get; } = new();

    public bool HasRecentProjects => RecentProjects.Count > 0;

    /// <summary>侧边栏页脚：版本 + 「数据留在本机」（替代原来的纯标语，VD-082）。</summary>
    public string VersionLine =>
        string.Format(_localization.GetString("App_VersionLine"), Updates.CurrentVersionText);

    /// <summary>打开最近立项（侧边栏条目点击）。</summary>
    public ICommand OpenRecentCommand { get; }

    public void Initialize()
    {
        // 首个页面就是列表：这里必须先落进 _listViewModel，否则首次 ShowProjectsAsync
        // 会再建一个新实例，用户第一次「进详情再返回」时筛选与滚动位置照样丢（VD-079）。
        _listViewModel = CreateListView();
        _navigation.NavigateTo(_listViewModel);
    }

    /// <summary>
    /// 返回立项列表（VD-079）。
    /// 列表视图模型在会话内复用同一个实例：搜索词、类型/状态筛选、滚动位置都留在实例里，
    /// 从详情页返回时只刷新数据，不再把用户的筛选条件清空、也不再跳回列表顶部。
    /// </summary>
    public async Task ShowProjectsAsync()
    {
        if (_listViewModel is null)
        {
            _listViewModel = CreateListView();
        }
        else
        {
            // 从详情 / 向导回来时总数可能已变（新建或删除），标记后这里会重取一次
            _listViewModel.MarkTotalDirty();
            await _listViewModel.LoadAsync();
        }

        await NavigateAsync(() => _listViewModel!);
    }

    public Task ShowNewProjectAsync() => NavigateAsync(() => new ProjectWizardViewModel(
        _projectService,
        _localization,
        () => _ = ShowProjectsAsync(),
        _dialogService));

    public Task ShowSettingsAsync() => NavigateAsync(() => new SettingsViewModel(_themeService, _localization, Updates));

    /// <summary>关闭窗口前的守卫：详情页有未保存更改时先询问用户（A12）。</summary>
    public Task<bool> CanCloseAsync() => ConfirmLeaveCurrentAsync();

    /// <summary>直接打开指定立项的详情页（截图 / 预览脚本用）。</summary>
    internal void ShowDetail(Project project) => OpenDetail(project);

    private async Task NavigateAsync(Func<ViewModelBase> factory)
    {
        if (!await ConfirmLeaveCurrentAsync())
        {
            return;
        }

        _navigation.NavigateTo(factory());
    }

    private async Task<bool> ConfirmLeaveCurrentAsync() => _navigation.Current switch
    {
        ProjectDetailViewModel detail => await detail.TryLeaveAsync(),
        // VD-092：向导也纳入离开保护（填了一半被侧边栏导航清空，此前没有任何提示）
        ProjectWizardViewModel wizard => await wizard.ConfirmLeaveAsync(),
        _ => true,
    };

    private ProjectListViewModel CreateListView() =>
        new(_projectService, _localization, OpenDetail, () => _ = ShowNewProjectAsync());

    /// <summary>打开侧边栏的最近立项（先走未保存守卫，规则与其它导航一致）。</summary>
    private async Task OpenRecentAsync(object? parameter)
    {
        if (parameter is not Project project || !await ConfirmLeaveCurrentAsync())
        {
            return;
        }

        OpenDetail(project);
    }

    /// <summary>
    /// 侧边栏最近立项：取更新时间最新的 4 条（VD-082）。
    /// 每次导航后刷新一次，数据量小，单次查询可忽略。
    /// </summary>
    private async Task RefreshRecentAsync()
    {
        try
        {
            var all = await _projectService.GetAllAsync();
            var recent = all
                .OrderByDescending(p => p.UpdatedAt)
                .Take(4)
                .ToList();

            RecentProjects.Clear();
            foreach (var project in recent)
            {
                RecentProjects.Add(project);
            }

            OnPropertyChanged(nameof(HasRecentProjects));
        }
        catch (Exception ex)
        {
            // 侧边栏是辅助信息，读失败不影响主流程，只记日志
            Services.UpdateService.Log($"侧边栏最近立项加载失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 覆盖层内容变化（VD-063）：打开立即生效，关闭则先播 140ms 退场动画再卸载。
    /// 退场期间若又打开了新弹窗，用版本号作废这次延迟，避免把新弹窗一起关掉。
    /// </summary>
    private async void OnActiveDialogChanged(object? content)
    {
        if (content is not null)
        {
            _dialogCloseVersion++;
            IsDialogClosing = false;
            DialogContent = content;
            OnPropertyChanged(nameof(IsDialogOpen));
            return;
        }

        if (_dialogContent is null)
        {
            return;
        }

        var version = ++_dialogCloseVersion;
        IsDialogClosing = true;

        try
        {
            await Task.Delay(140);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (version != _dialogCloseVersion)
        {
            return;
        }

        DialogContent = null;
        IsDialogClosing = false;
        OnPropertyChanged(nameof(IsDialogOpen));
    }

    private void OpenDetail(Project project) =>
        _navigation.NavigateTo(new ProjectDetailViewModel(
            _projectService,
            _localization,
            _dialogService,
            _documentRepository,
            _importService,
            _gitService,
            _filePicker,
            project,
            () => _ = ShowProjectsAsync()));

    private void CloseDialog(bool isScrimClick)
    {
        // VD-083：有输入的配置类弹窗不接受「点遮罩关闭」（误点即丢）
        if (isScrimClick && DialogContent is IOverlayDialogViewModel { AllowScrimClose: false })
        {
            return;
        }

        if (DialogContent is ConfirmDialogViewModel vm
            && vm.CancelCommand.CanExecute(null))
        {
            vm.CancelCommand.Execute(null);
        }
        else if (DialogContent is UnsavedChangesDialogViewModel unsaved
                 && unsaved.CancelCommand.CanExecute(null))
        {
            unsaved.CancelCommand.Execute(null);
        }
        else if (DialogContent is AddRemoteDialogViewModel remote)
        {
            // VD-083：Esc 对已输入的配置弹窗只做「第一次拦下 + 提示」，第二次才真正放弃
            if (remote.ConfirmEsc())
            {
                remote.RequestClose();
            }
        }
        else if (DialogContent is IOverlayDialogViewModel overlay)
        {
            overlay.RequestClose();
        }
    }
}
