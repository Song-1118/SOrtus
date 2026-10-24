using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using SOrtus.Common;
using SOrtus.Core.Models;
using SOrtus.Core.Services;

namespace SOrtus.ViewModels;

/// <summary>
/// 立项列表视图模型（规范 F0-02 / A7）：展示、按类型/状态筛选、按关键字搜索。
/// 列表外层容器使用磨砂卡片；列表项不带独立模糊（规范 7.7）。
/// 空状态区分「暂无立项」（引导新建）与「无匹配结果」（引导调整筛选）。
/// </summary>
internal sealed class ProjectListViewModel : ViewModelBase
{
    private readonly IProjectService _service;
    private readonly SOrtus.Core.Services.ILocalizationService _localization;
    private readonly Action<Project> _openDetail;
    private readonly Action _requestNew;

    private string _searchText = string.Empty;
    private ProjectType? _filterType;
    private ProjectStatus? _filterStatus;
    private Project? _selectedProject;
    private int _totalCount;

    public ObservableCollection<Project> Projects { get; } = new();

    public ObservableCollection<ProjectType?> TypeOptions { get; }
    public ObservableCollection<ProjectStatus?> StatusOptions { get; }

    public ICommand NewCommand { get; }
    public ICommand OpenDetailCommand { get; }
    public ICommand RefreshCommand { get; }

    /// <summary>立项总数（不受筛选影响，用于副标题统计）。</summary>
    public int TotalCount
    {
        get => _totalCount;
        private set => SetProperty(ref _totalCount, value);
    }

    /// <summary>本地化副标题（「共 N 个立项 · 数据只留在本机」）。</summary>
    public string Subtitle =>
        string.Format(_localization.GetString("List_Subtitle"), TotalCount);

    /// <summary>是否处于空列表（无任何立项）。</summary>
    public bool IsEmpty => Projects.Count == 0 && TotalCount == 0;

    /// <summary>是否应用了搜索 / 筛选（区分两种空状态文案）。</summary>
    public bool HasFilter =>
        !string.IsNullOrWhiteSpace(_searchText) || _filterType.HasValue || _filterStatus.HasValue;

    /// <summary>有数据但被筛选过滤为空（区别于「完全没有任何立项」）。</summary>
    public bool HasNoResults => Projects.Count == 0 && TotalCount > 0;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                OnPropertyChanged(nameof(HasFilter));
                _ = ReloadAsync();
            }
        }
    }

    public ProjectType? FilterType
    {
        get => _filterType;
        set
        {
            if (SetProperty(ref _filterType, value))
            {
                OnPropertyChanged(nameof(HasFilter));
                _ = ReloadAsync();
            }
        }
    }

    public ProjectStatus? FilterStatus
    {
        get => _filterStatus;
        set
        {
            if (SetProperty(ref _filterStatus, value))
            {
                OnPropertyChanged(nameof(HasFilter));
                _ = ReloadAsync();
            }
        }
    }

    public Project? SelectedProject
    {
        get => _selectedProject;
        set => SetProperty(ref _selectedProject, value);
    }

    public ProjectListViewModel(IProjectService service, SOrtus.Core.Services.ILocalizationService localization, Action<Project> openDetail, Action requestNew)
    {
        _service = service;
        _localization = localization;
        _openDetail = openDetail;
        _requestNew = requestNew;

        TypeOptions = new ObservableCollection<ProjectType?> { null, ProjectType.Programming, ProjectType.Clip, ProjectType.Todo };
        StatusOptions = new ObservableCollection<ProjectStatus?> { null, ProjectStatus.Planning, ProjectStatus.Active, ProjectStatus.Completed };

        NewCommand = new RelayCommand(_ => _requestNew());
        OpenDetailCommand = new RelayCommand(o => { if (o is Project p) _openDetail(p); });
        RefreshCommand = new AsyncRelayCommand(LoadAsync);

        _ = LoadAsync();
    }

    private async Task ReloadAsync() => await LoadAsync();

    /// <summary>加载 / 刷新列表（筛选条件变化与外部刷新共用；测试可直接 await）。</summary>
    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            var keyword = string.IsNullOrWhiteSpace(_searchText) ? null : _searchText;
            var items = await _service.QueryAsync(_filterType, _filterStatus, keyword);
            var all = await _service.GetAllAsync();

            void Apply()
            {
                Projects.Clear();
                foreach (var project in items)
                {
                    Projects.Add(project);
                }

                TotalCount = all.Count;
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(HasFilter));
                OnPropertyChanged(nameof(HasNoResults));
                OnPropertyChanged(nameof(Subtitle));
            }

            // 无 UI 宿主（单元测试）时直接更新；有宿主时切回 UI 线程（ObservableCollection 不能跨线程改）
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                Apply();
            }
            else
            {
                await dispatcher.InvokeAsync(Apply);
            }
        });
    }
}
