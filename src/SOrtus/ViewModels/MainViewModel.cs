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
        };
        _dialogService.ActiveDialogChanged += OnActiveDialogChanged;

        _navigateProjectsCommand = new AsyncRelayCommand(() => ShowProjectsAsync());
        _navigateNewCommand = new AsyncRelayCommand(() => ShowNewProjectAsync());
        _navigateSettingsCommand = new AsyncRelayCommand(() => ShowSettingsAsync());
        _closeDialogCommand = new RelayCommand(_ => CloseDialog());

        Updates = new UpdatesViewModel(dialogService, localization);
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
            await _listViewModel.LoadAsync();
        }

        await NavigateAsync(() => _listViewModel!);
    }

    public Task ShowNewProjectAsync() => NavigateAsync(() => new ProjectWizardViewModel(_projectService, _localization, () => _ = ShowProjectsAsync()));

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

    private async Task<bool> ConfirmLeaveCurrentAsync() =>
        _navigation.Current is ProjectDetailViewModel detail
            ? await detail.TryLeaveAsync()
            : true;

    private ProjectListViewModel CreateListView() =>
        new(_projectService, _localization, OpenDetail, () => _ = ShowNewProjectAsync());

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

    private void CloseDialog()
    {
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
        else if (DialogContent is IOverlayDialogViewModel overlay)
        {
            overlay.RequestClose();
        }
    }
}
