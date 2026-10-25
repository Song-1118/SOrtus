using System.Collections.ObjectModel;
using System.Threading;
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
    private bool _totalDirty = true;
    private CancellationTokenSource? _searchCts;
    private int _loadVersion;

    /// <summary>搜索防抖窗口（VD-086：中文输入法一个词会连发 4~6 次 PropertyChanged）。</summary>
    internal const int SearchDebounceMs = 250;

    public ObservableCollection<Project> Projects { get; } = new();

    public ObservableCollection<ProjectType?> TypeOptions { get; }
    public ObservableCollection<ProjectStatus?> StatusOptions { get; }

    public ICommand NewCommand { get; }
    public ICommand OpenDetailCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ClearFiltersCommand { get; }

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

    /// <summary>
    /// 「没有匹配的立项（共 N 个）」——带上总数，用户才知道筛掉的是 3 个还是 300 个（VD-087）。
    /// </summary>
    public string NoResultsText =>
        string.Format(_localization.GetString("List_NoResultsCount"), TotalCount);

    /// <summary>一键清除全部筛选条件（VD-087：空结果页的唯一出口，不再让用户逐个改回「全部」）。</summary>
    private void ClearFilters()
    {
        if (!HasFilter)
        {
            return;
        }

        // 直接改字段再统一刷新一次，避免三个 setter 各触发一次查询
        _searchText = string.Empty;
        _filterType = null;
        _filterStatus = null;

        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(FilterType));
        OnPropertyChanged(nameof(FilterStatus));
        OnPropertyChanged(nameof(HasFilter));
        OnPropertyChanged(nameof(IsSearchActive));

        _ = ReloadNowAsync();
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                OnPropertyChanged(nameof(HasFilter));
                OnPropertyChanged(nameof(IsSearchActive));
                ScheduleSearchReload();
            }
        }
    }

    /// <summary>搜索框是否有内容（VD-085：有内容时显示清除 ×）。</summary>
    public bool IsSearchActive => !string.IsNullOrWhiteSpace(_searchText);

    /// <summary>清空搜索词（搜索框内的 ×）。</summary>
    public ICommand ClearSearchCommand { get; }

    public ProjectType? FilterType
    {
        get => _filterType;
        set
        {
            if (SetProperty(ref _filterType, value))
            {
                OnPropertyChanged(nameof(HasFilter));
                _ = ReloadNowAsync();
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
                _ = ReloadNowAsync();
            }
        }
    }

    /// <summary>
    /// 列表滚动位置（VD-079）。由 ViewTemplates 里的 ScrollViewer 双向绑定：
    /// 离开列表时写回，返回时还原，用户不会因为去看了眼详情就回到顶部。
    /// </summary>
    public double ScrollOffset { get; set; }

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
        StatusOptions = new ObservableCollection<ProjectStatus?>
        {
            null,
            ProjectStatus.Planning,
            ProjectStatus.Active,
            ProjectStatus.OnHold,
            ProjectStatus.Completed,
            ProjectStatus.Archived,
        };

        NewCommand = new RelayCommand(_ => _requestNew());
        OpenDetailCommand = new RelayCommand(o => { if (o is Project p) _openDetail(p); });
        RefreshCommand = new AsyncRelayCommand(() => LoadAsync());
        ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
        ClearSearchCommand = new RelayCommand(_ => SearchText = string.Empty);

        _ = LoadAsync();
    }

    /// <summary>总数需要重取（新建 / 删除后由主视图模型调用，VD-086）。</summary>
    public void MarkTotalDirty() => _totalDirty = true;

    /// <summary>立即加载（筛选条件变化、手动刷新、清除筛选）：作废尚未触发的防抖。</summary>
    private async Task ReloadNowAsync()
    {
        _searchCts?.Cancel();
        await LoadAsync();
    }

    /// <summary>搜索防抖（VD-086）：250ms 内的连续击键只查最后一次。</summary>
    private void ScheduleSearchReload()
    {
        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;
        _ = DebouncedReloadAsync(cts.Token);
    }

    private async Task DebouncedReloadAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(SearchDebounceMs, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await LoadAsync(token);
    }

    /// <summary>加载 / 刷新列表（筛选条件变化与外部刷新共用；测试可直接 await）。</summary>
    public async Task LoadAsync(CancellationToken token = default)
    {
        var version = ++_loadVersion;

        await RunBusyAsync(async () =>
        {
            var keyword = string.IsNullOrWhiteSpace(_searchText) ? null : _searchText;
            var items = await _service.QueryAsync(_filterType, _filterStatus, keyword);

            // 过期结果直接丢弃：连续输入时后到的旧结果会把新结果盖掉，列表看起来在闪跳
            if (token.IsCancellationRequested || version != _loadVersion)
            {
                return;
            }

            // TotalCount 只在必要时取（VD-086）：无筛选时查询结果本身就是全集，
            // 有筛选时用缓存，只有新建 / 删除后（MarkTotalDirty）才重取一次。
            int total;
            if (!HasFilter)
            {
                total = items.Count;
                _totalDirty = false;
            }
            else if (_totalDirty || _totalCount == 0)
            {
                var all = await _service.GetAllAsync();
                total = all.Count;
                _totalDirty = false;
            }
            else
            {
                total = _totalCount;
            }

            void Apply()
            {
                Projects.Clear();
                foreach (var project in items)
                {
                    Projects.Add(project);
                }

                TotalCount = total;
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(HasFilter));
                OnPropertyChanged(nameof(HasNoResults));
                OnPropertyChanged(nameof(NoResultsText));
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
