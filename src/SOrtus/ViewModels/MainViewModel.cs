using System.Windows.Input;
using SOrtus.Common;
using SOrtus.Core.Models;
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

    private readonly AsyncRelayCommand _navigateProjectsCommand;
    private readonly AsyncRelayCommand _navigateNewCommand;
    private readonly AsyncRelayCommand _navigateSettingsCommand;
    private readonly RelayCommand _closeDialogCommand;

    private object? _dialogContent;

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

    public MainViewModel(
        IProjectService projectService,
        IThemeService themeService,
        ILocalizationService localization,
        INavigationService navigation,
        IDialogService dialogService)
    {
        _projectService = projectService;
        _themeService = themeService;
        _localization = localization;
        _navigation = navigation;
        _dialogService = dialogService;
        _navigation.Navigated += (_, _) =>
        {
            OnPropertyChanged(nameof(CurrentView));
            OnPropertyChanged(nameof(ActiveNav));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        };
        _dialogService.ActiveDialogChanged += content =>
        {
            DialogContent = content;
            OnPropertyChanged(nameof(IsDialogOpen));
        };

        _navigateProjectsCommand = new AsyncRelayCommand(() => ShowProjectsAsync());
        _navigateNewCommand = new AsyncRelayCommand(() => ShowNewProjectAsync());
        _navigateSettingsCommand = new AsyncRelayCommand(() => ShowSettingsAsync());
        _closeDialogCommand = new RelayCommand(_ => CloseDialog());
    }

    public ICommand NavigateProjectsCommand => _navigateProjectsCommand;
    public ICommand NavigateNewCommand => _navigateNewCommand;
    public ICommand NavigateSettingsCommand => _navigateSettingsCommand;

    /// <summary>关闭对话框（遮罩点击 / Esc，等同取消）。</summary>
    public ICommand CloseDialogCommand => _closeDialogCommand;

    public void Initialize() => _navigation.NavigateTo(CreateListView());

    public Task ShowProjectsAsync() => NavigateAsync(() => CreateListView());

    public Task ShowNewProjectAsync() => NavigateAsync(() => new ProjectWizardViewModel(_projectService, _localization, () => _ = ShowProjectsAsync()));

    public Task ShowSettingsAsync() => NavigateAsync(() => new SettingsViewModel(_themeService, _localization));

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

    private void OpenDetail(Project project) =>
        _navigation.NavigateTo(new ProjectDetailViewModel(_projectService, _localization, _dialogService, project, () => _ = ShowProjectsAsync()));

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
    }
}
