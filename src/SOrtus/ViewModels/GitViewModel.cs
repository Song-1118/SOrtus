using System.Collections.ObjectModel;
using System.Windows.Input;
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
                Apply(await _git.InspectAsync(folder));
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
                Apply(await _git.InspectAsync(_folderPathProvider()));
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

        Timeline.Clear();
        var index = 0;
        foreach (var commit in info.RecentCommits)
        {
            Timeline.Add(new GitTimelineItemViewModel(commit, isFirst: index == 0, isLast: index == info.RecentCommits.Count - 1));
            index++;
        }

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
