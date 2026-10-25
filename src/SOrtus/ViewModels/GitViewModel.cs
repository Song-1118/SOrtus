using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using SOrtus.Common;
using SOrtus.Controls;
using SOrtus.Core.Models;
using SOrtus.Core.Services;
using SOrtus.Services;

namespace SOrtus.ViewModels;

/// <summary>
/// GIT 管理版块视图模型（规范 1.5：编程类型的分支时间线与推送）。
/// <para>
/// 只读展示 + 两个动作：配置远端、推送。首次进入版块才加载（<see cref="ActivateAsync"/> 懒加载），
/// 推送成功后重新拉取快照，使 ahead/behind 与时间线回到最新状态。
/// </para>
/// </summary>
internal sealed class GitViewModel : ViewModelBase
{
    private readonly IGitService _git;
    private readonly ILocalizationService _localization;
    private readonly IDialogService _dialogService;
    private readonly Func<string> _folderPathProvider;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _pushCommand;
    private readonly AsyncRelayCommand _addRemoteCommand;

    private GitRepositoryInfo _info = GitRepositoryInfo.NotARepository;
    private bool _loaded;
    private string? _statusMessage;
    private string? _errorMessage;
    private string? _rawOutput;

    /// <summary>时间线分页（VD-107）：默认 10 条，点「查看更早」每次再取 10 条。</summary>
    private const int TimelinePageSize = 10;

    /// <summary>一次快照最多抓多少条提交。不足此数即「这就是全部提交」。</summary>
    private const int TimelineWindow = 50;

    private readonly List<GitCommitInfo> _allCommits = new();
    private int _visibleCommitCount = TimelinePageSize;

    public GitViewModel(
        IGitService git,
        ILocalizationService localization,
        IDialogService dialogService,
        Project project)
    {
        _git = git ?? throw new ArgumentNullException(nameof(git));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _folderPathProvider = () => project.FolderPath ?? string.Empty;

        _refreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        _pushCommand = new AsyncRelayCommand(PushAsync, () => !IsBusy && HasRemote && IsRepository);
        _addRemoteCommand = new AsyncRelayCommand(AddRemoteAsync, () => !IsBusy && IsRepository);
        ShowMoreCommitsCommand = new RelayCommand(_ => ShowMoreCommits(), _ => HasMoreCommits);
    }

    public ObservableCollection<GitBranchItemViewModel> Branches { get; } = new();

    public ObservableCollection<GitTimelineItemViewModel> Timeline { get; } = new();

    public ObservableCollection<string> Remotes { get; } = new();

    public bool IsRepository => _info.IsRepository;

    public bool IsNotRepository => !_info.IsRepository;

    public bool HasRemote => _info.HasRemote;

    public bool NoRemote => _info.IsRepository && !_info.HasRemote;

    public bool HasUpstream => _info.HasUpstream;

    public bool HasAhead => _info.Ahead > 0;

    public bool HasBehind => _info.Behind > 0;

    public string AheadText => $"+{_info.Ahead}";

    public string BehindText => $"-{_info.Behind}";

    public string CurrentBranch =>
        string.IsNullOrEmpty(_info.CurrentBranch)
            ? _localization.GetString("Git_DetachedHead")
            : _info.CurrentBranch;

    public string WorkTreeRoot => _info.WorkTreeRoot;

    public string DirectoryText => _folderPathProvider();

    public bool HasCommits => Timeline.Count > 0;

    public bool IsEmptyRepository => _info.IsRepository && Timeline.Count == 0;

    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrEmpty(_statusMessage);

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_errorMessage);

    public string? RawOutput
    {
        get => _rawOutput;
        private set
        {
            if (SetProperty(ref _rawOutput, value))
            {
                OnPropertyChanged(nameof(HasRawOutput));
            }
        }
    }

    public bool HasRawOutput => !string.IsNullOrEmpty(_rawOutput);

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand PushCommand => _pushCommand;

    public ICommand AddRemoteCommand => _addRemoteCommand;

    /// <summary>展开更早的提交（VD-107）。</summary>
    public ICommand ShowMoreCommitsCommand { get; }

    /// <summary>还有更早的提交没显示出来。</summary>
    public bool HasMoreCommits => _allCommits.Count > _visibleCommitCount;

    /// <summary>「查看更早的 N 条」。</summary>
    public string MoreCommitsText => string.Format(
        _localization.GetString("Git_TimelineMore"),
        Math.Min(TimelinePageSize, _allCommits.Count - _visibleCommitCount));

    /// <summary>
    /// 时间线末尾计数（VD-107）。
    /// 一次快照最多抓 <see cref="TimelineWindow"/> 条：抓不满说明这就是全部提交，给「共 N 条」；
    /// 抓满了就只能说「已显示最近 N 条」，不能把窗口当成总数——那是假信息。
    /// </summary>
    public string TimelineSummaryText
    {
        get
        {
            if (_allCommits.Count == 0)
            {
                return string.Empty;
            }

            var last = _allCommits[0].When == DateTimeOffset.MinValue
                ? string.Empty
                : _allCommits[0].When.ToLocalTime().ToString("MM-dd HH:mm");
            var key = _allCommits.Count < TimelineWindow ? "Git_TimelineCount" : "Git_TimelineCountWindow";
            return string.Format(_localization.GetString(key), _allCommits.Count, last);
        }
    }

    private void ShowMoreCommits()
    {
        _visibleCommitCount = Math.Min(_visibleCommitCount + TimelinePageSize, _allCommits.Count);
        RebuildTimeline();
    }

    private void RebuildTimeline()
    {
        Timeline.Clear();
        for (var i = 0; i < _visibleCommitCount && i < _allCommits.Count; i++)
        {
            Timeline.Add(new GitTimelineItemViewModel(
                _allCommits[i],
                isFirst: i == 0,
                isLast: i == _allCommits.Count - 1 || i == _visibleCommitCount - 1));
        }

        OnPropertyChanged(nameof(HasMoreCommits));
        OnPropertyChanged(nameof(MoreCommitsText));
        OnPropertyChanged(nameof(TimelineSummaryText));
        OnPropertyChanged(nameof(HasCommits));
        OnPropertyChanged(nameof(IsEmptyRepository));
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>首次进入 GIT 版块时加载（重复调用无副作用）。</summary>
    public Task ActivateAsync()
    {
        if (_loaded)
        {
            return Task.CompletedTask;
        }

        _loaded = true;
        return RefreshAsync();
    }

    /// <summary>关联目录变更后强制重载。</summary>
    public Task ReloadAsync()
    {
        _loaded = true;
        return RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var folder = _folderPathProvider();
        if (string.IsNullOrWhiteSpace(folder))
        {
            Apply(GitRepositoryInfo.NotARepository);
            ErrorMessage = _localization.GetString("Git_NoFolder");
            return;
        }

        await RunBusyAsync(async () =>
        {
            ErrorMessage = null;
            try
            {
                Apply(await _git.InspectAsync(folder, TimelineWindow));
                if (!_info.IsRepository)
                {
                    ErrorMessage = _localization.GetString("Git_NotRepository");
                }
            }
            catch (Exception ex)
            {
                Apply(GitRepositoryInfo.NotARepository);
                ErrorMessage = ex.Message;
            }
        });
    }

    private async Task PushAsync()
    {
        if (!_info.HasRemote)
        {
            ErrorMessage = _localization.GetString("Git_NoRemote");
            return;
        }

        var remote = _info.Remotes.FirstOrDefault()?.Name ?? "origin";
        await RunBusyAsync(async () =>
        {
            ErrorMessage = null;
            StatusMessage = null;
            RawOutput = null;

            var result = await _git.PushAsync(
                _folderPathProvider(),
                _info.CurrentBranch,
                remote,
                setUpstream: !_info.HasUpstream);

            if (result.Success)
            {
                StatusMessage = _localization.GetString("Git_PushSuccess");
                Apply(await _git.InspectAsync(_folderPathProvider(), TimelineWindow));
            }
            else
            {
                ErrorMessage = DescribeFailure(result);
                RawOutput = string.IsNullOrWhiteSpace(result.RawOutput) ? result.Message : result.RawOutput;
            }
        });
    }

    private async Task AddRemoteAsync()
    {
        var folder = _folderPathProvider();
        if (string.IsNullOrWhiteSpace(folder))
        {
            ErrorMessage = _localization.GetString("Git_NoFolder");
            return;
        }

        var existing = _info.Remotes.FirstOrDefault();
        var dialog = new AddRemoteDialogViewModel(
            _git,
            folder,
            existing?.Name ?? "origin",
            existing?.Url ?? string.Empty,
            _localization);

        await _dialogService.ShowAsync(dialog);

        if (dialog.Result is not null)
        {
            StatusMessage = _localization.GetString("Git_RemoteSaved");
            await RefreshAsync();
        }
    }

    private string DescribeFailure(GitOperationResult result)
    {
        var key = result.Kind switch
        {
            GitFailureKind.NotInstalled => "Git_ErrNotInstalled",
            GitFailureKind.NotARepository => "Git_NotRepository",
            GitFailureKind.NoRemote => "Git_NoRemote",
            GitFailureKind.NoUpstream => "Git_NoUpstream",
            GitFailureKind.AuthFailed => "Git_ErrAuth",
            GitFailureKind.NonFastForward => "Git_ErrNonFastForward",
            GitFailureKind.Conflict => "Git_ErrConflict",
            GitFailureKind.Timeout => "Git_ErrTimeout",
            _ => null,
        };

        if (key is null)
        {
            return string.IsNullOrWhiteSpace(result.Message)
                ? _localization.GetString("Git_ErrUnknown")
                : result.Message;
        }

        var localized = _localization.GetString(key);
        return string.IsNullOrWhiteSpace(result.Message) || localized == key
            ? localized
            : $"{localized}（{result.Message}）";
    }

    private void Apply(GitRepositoryInfo info)
    {
        _info = info;

        Branches.Clear();
        foreach (var branch in info.Branches)
        {
            Branches.Add(new GitBranchItemViewModel(branch));
        }

        // VD-107：时间线默认只渲染 10 条，剩下放进「查看更早」——
        // 旧版一次性铺满（长仓库上百条），卡片下方还要留 400px 空白，
        // 用户既不知道「是只有这些提交」还是「被截断了」，首屏也被拖慢。
        _allCommits.Clear();
        _allCommits.AddRange(info.RecentCommits);
        _visibleCommitCount = Math.Min(TimelinePageSize, _allCommits.Count);
        RebuildTimeline();

        Remotes.Clear();
        foreach (var remote in info.Remotes)
        {
            Remotes.Add($"{remote.Name} · {remote.Url}");
        }

        foreach (var name in new[]
                 {
                     nameof(IsRepository), nameof(IsNotRepository), nameof(HasRemote), nameof(NoRemote),
                     nameof(HasUpstream), nameof(HasAhead), nameof(HasBehind), nameof(AheadText),
                     nameof(BehindText), nameof(CurrentBranch), nameof(WorkTreeRoot), nameof(HasCommits),
                     nameof(IsEmptyRepository), nameof(DirectoryText),
                 })
        {
            OnPropertyChanged(name);
        }

        // 推送 / 添加远端的可用性取决于仓库与远端状态，刷新后需要重新询问一次
        CommandManager.InvalidateRequerySuggested();
    }
}

/// <summary>分支标签（本地/远程、是否当前）。</summary>
internal sealed class GitBranchItemViewModel
{
    public GitBranchItemViewModel(GitBranchInfo branch)
    {
        Name = branch.Name;
        IsCurrent = branch.IsCurrent;
        IsRemote = branch.IsRemote;
        TipSubject = branch.TipSubject;
        Tone = branch.IsRemote
            ? SOrTagTone.Neutral
            : branch.IsCurrent ? SOrTagTone.AccentB : SOrTagTone.AccentA;
    }

    public string Name { get; }

    public bool IsCurrent { get; }

    public bool IsRemote { get; }

    public string TipSubject { get; }

    public SOrTagTone Tone { get; }
}

/// <summary>时间线节点（提交）。</summary>
internal sealed class GitTimelineItemViewModel
{
    public GitTimelineItemViewModel(GitCommitInfo commit, bool isFirst, bool isLast)
    {
        ShortSha = commit.ShortSha;
        Subject = string.IsNullOrWhiteSpace(commit.Subject) ? "（无提交信息）" : commit.Subject;
        Author = commit.Author;
        WhenText = commit.When == DateTimeOffset.MinValue
            ? string.Empty
            : commit.When.ToLocalTime().ToString("MM-dd HH:mm");
        Refs = commit.Refs;
        IsFirst = isFirst;
        IsLast = isLast;
    }

    public string ShortSha { get; }

    public string Subject { get; }

    public string Author { get; }

    public string WhenText { get; }

    public IReadOnlyList<string> Refs { get; }

    public bool IsFirst { get; }

    public bool IsLast { get; }

    public bool HasRefs => Refs.Count > 0;

    public bool ShowTopLine => !IsFirst;

    public bool ShowBottomLine => !IsLast;
}

/// <summary>
/// 配置远端对话框（规范：无远端时推送按钮禁用，引导用户先配置地址）。
/// </summary>
internal sealed class AddRemoteDialogViewModel : ViewModelBase, IClosableOverlayDialogViewModel
{
    private readonly IGitService _git;
    private readonly ILocalizationService _localization;
    private readonly string _folderPath;
    private readonly RelayCommand _closeCommand;

    private string _name;
    private string _url;
    private string? _errorMessage;

    public AddRemoteDialogViewModel(
        IGitService git,
        string folderPath,
        string name,
        string url,
        ILocalizationService localization)
    {
        _git = git;
        _folderPath = folderPath;
        _localization = localization;
        _name = string.IsNullOrWhiteSpace(name) ? "origin" : name;
        _url = url;
        _closeCommand = new RelayCommand(RequestClose);
        ConfirmCommand = new AsyncRelayCommand(ConfirmAsync, () => !IsBusy);
    }

    public event Action? Closed;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_errorMessage);

    /// <summary>
    /// VD-083：配置远端弹窗不响应「点遮罩关闭」。
    /// 遮罩点击与 Esc 共用同一入口时，用户在输入框里打字误点到外面就会丢掉整段 URL，
    /// 且没有草稿可恢复——这类有输入的弹窗只保留 Esc（带确认）与「取消」按钮。
    /// </summary>
    public bool AllowScrimClose => false;

    /// <summary>是否已有输入（Esc 关闭前据此决定要不要二次确认）。</summary>
    public bool HasInput =>
        !string.IsNullOrWhiteSpace(_url) || !string.Equals(_name, "origin", StringComparison.Ordinal);

    /// <summary>Esc 拦截提示是否可见（第一次按 Esc 时出现，3 秒后自动收起）。</summary>
    public bool ShowEscHint
    {
        get => _showEscHint;
        private set => SetProperty(ref _showEscHint, value);
    }

    private bool _showEscHint;
    private bool _escArmed;
    private DispatcherTimer? _escTimer;

    /// <summary>
    /// Esc 处理（VD-083）：有输入时第一次只拦下并提示「再按一次放弃」，第二次才允许关闭。
    /// 之所以不弹二次确认框：确认框会替换掉覆盖层里的当前弹窗（同一时刻只有一个活动对话框），
    /// 关掉确认框时这个弹窗也会一起消失，用户输入照样丢。
    /// </summary>
    public bool ConfirmEsc()
    {
        if (!HasInput || _escArmed)
        {
            _escArmed = false;
            ShowEscHint = false;
            return true;
        }

        _escArmed = true;
        ShowEscHint = true;

        _escTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _escTimer.Stop();
        _escTimer.Tick -= OnEscTimerTick;
        _escTimer.Tick += OnEscTimerTick;
        _escTimer.Start();

        return false;
    }

    private void OnEscTimerTick(object? sender, EventArgs e)
    {
        _escTimer?.Stop();
        _escArmed = false;
        ShowEscHint = false;
    }

    public GitRemoteInfo? Result { get; private set; }

    public ICommand ConfirmCommand { get; }

    public ICommand CloseCommand => _closeCommand;

    public void RequestClose() => Closed?.Invoke();

    private async Task ConfirmAsync()
    {
        var name = Name?.Trim() ?? string.Empty;
        var url = Url?.Trim() ?? string.Empty;

        if (name.Length == 0)
        {
            ErrorMessage = _localization.GetString("Git_RemoteNameRequired");
            return;
        }

        if (!IsLikelyUrl(url))
        {
            ErrorMessage = _localization.GetString("Git_RemoteUrlInvalid");
            return;
        }

        await RunBusyAsync(async () =>
        {
            ErrorMessage = null;
            var result = await _git.AddOrSetRemoteAsync(_folderPath, name, url);
            if (result.Success)
            {
                Result = new GitRemoteInfo(name, url);
                RequestClose();
            }
            else
            {
                ErrorMessage = result.Message;
            }
        });
    }

    /// <summary>远端地址只做形态校验（http/https/ssh/git/file 或 user@host:path），不做网络探测。</summary>
    private static bool IsLikelyUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
               || url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase)
               || url.StartsWith("git://", StringComparison.OrdinalIgnoreCase)
               || url.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
               || (url.Contains('@') && url.Contains(':'));
    }
}
