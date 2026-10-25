using System.Globalization;
using System.Windows;
using System.Windows.Input;
using SOrtus.Common;
using SOrtus.Core.Services;
using SOrtus.Services;
using SOrtus.Updates;

namespace SOrtus.ViewModels;

/// <summary>
/// 更新详情覆盖层：展示版本差异与更新说明，并在用户确认后完成
/// 「下载 → SHA256 校验 → 备份数据 → 启动静默安装器 → 退出自身」全流程。
/// <para>
/// 之所以把整个流程放在这个对话框里而不是后台任务：用户必须能看到「正在下载 43%」
/// 和「更新失败：XXX」这两种状态。静默后台下载一旦失败，用户只会觉得软件莫名其妙变慢。
/// </para>
/// </summary>
internal sealed class UpdateViewModel : ViewModelBase, IClosableOverlayDialogViewModel
{
    private readonly UpdateService _updates;
    private readonly ILocalizationService _localization;
    private readonly UpdateInfo _info;
    private readonly Action _shutdown;
    private CancellationTokenSource? _cancellation;

    private string _statusText = string.Empty;
    private double _progressValue;
    private bool _isProgressVisible;
    private bool _hasFailed;

    public UpdateViewModel(
        UpdateInfo info,
        UpdateService updates,
        ILocalizationService localization,
        Action? shutdown = null)
    {
        _info = info;
        _updates = updates;
        _localization = localization;
        _shutdown = shutdown ?? (() => Application.Current?.Shutdown());

        InstallCommand = new AsyncRelayCommand(_ => InstallAsync(), _ => !IsBusy);
        RetryCommand = new RelayCommand(_ => ResetAfterFailure());
        PrimaryActionCommand = new AsyncRelayCommand(_ => RunPrimaryAsync(), _ => !IsBusy);
        SkipCommand = new RelayCommand(_ => Skip());
        // VD-116：「稍后提醒」不能只是关闭——它承诺了「以后还会再提」，
        // 因此写入 snooze 时间窗，到期前启动检查发现同一版本就不再打扰。
        LaterCommand = new RelayCommand(_ => Snooze());
        CloseCommand = new RelayCommand(_ => RequestClose());
        OpenPageCommand = new RelayCommand(_ => OpenReleasePage(), _ => _info.ReleasePageUri is not null);

        StatusText = _localization.GetString("Update_InstallNow");
    }

    public event Action? Closed;

    public string Title => _localization.GetString("Update_AvailableTitle");

    /// <summary>版本对比行（当前 0.1.2 → 最新 0.2.0）。</summary>
    public string VersionText => string.Format(
        _localization.GetString("Update_VersionLine"),
        _updates.CurrentVersion,
        _info.Version);

    /// <summary>安装包体积行。0 表示发布端未提供 size，此时不显示。</summary>
    public string SizeText => _info.SizeBytes > 0
        ? string.Format(
            _localization.GetString("Update_SizeLine"),
            (_info.SizeBytes / 1024d / 1024d).ToString("F1", CultureInfo.InvariantCulture))
        : string.Empty;

    public bool IsSizeVisible => _info.SizeBytes > 0;

    public string NotesTitle => _localization.GetString("Update_NotesTitle");

    /// <summary>更新说明；发布端留空时给出明确占位，而不是留一块空白。</summary>
    public string NotesText => string.IsNullOrWhiteSpace(_info.ReleaseNotes)
        ? _localization.GetString("Update_NoNotes")
        : _info.ReleaseNotes;

    public string InstallText => _localization.GetString("Update_InstallNow");

    public string RetryText => _localization.GetString("Update_Retry");

    public string LaterText => _localization.GetString("Update_Later");

    public string SkipText => _localization.GetString("Update_Skip");

    public string OpenPageText => _localization.GetString("Update_OpenPage");

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>下载进度（0-100）。</summary>
    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public bool IsProgressVisible
    {
        get => _isProgressVisible;
        private set => SetProperty(ref _isProgressVisible, value);
    }

    public bool HasFailed
    {
        get => _hasFailed;
        private set
        {
            if (SetProperty(ref _hasFailed, value))
            {
                OnPropertyChanged(nameof(CanStartInstall));
                OnPropertyChanged(nameof(CanSkip));
                OnPropertyChanged(nameof(PrimaryActionText));
            }
        }
    }

    /// <summary>主按钮文案：正常情况下是「下载并安装」，失败后变「重试」。</summary>
    public string PrimaryActionText => HasFailed ? RetryText : InstallText;

    /// <summary>主按钮命令：失败态下先重置再重新下载。</summary>
    public ICommand PrimaryActionCommand { get; }

    /// <summary>未在下载中且未失败时，才允许发起安装。</summary>
    public bool CanStartInstall => !IsBusy && !HasFailed;

    public bool CanSkip => !IsBusy && !HasFailed;

    public ICommand InstallCommand { get; }

    public ICommand RetryCommand { get; }

    public ICommand SkipCommand { get; }

    public ICommand LaterCommand { get; }

    public ICommand CloseCommand { get; }

    public ICommand OpenPageCommand { get; }

    public void RequestClose()
    {
        if (IsBusy)
        {
            // 下载中不允许关闭：关掉对话框会让用户以为更新被取消，实际后台仍在写文件
            return;
        }

        _cancellation?.Cancel();
        Closed?.Invoke();
    }

    private void ResetAfterFailure()
    {
        HasFailed = false;
        IsProgressVisible = false;
        ProgressValue = 0;
        StatusText = _localization.GetString("Update_InstallNow");
    }

    /// <summary>主按钮入口：失败后重试即从干净状态重新下载。</summary>
    private async Task RunPrimaryAsync()
    {
        if (HasFailed)
        {
            ResetAfterFailure();
        }

        await InstallAsync();
    }

    private void Skip()
    {
        _updates.SkipVersion(_info.Version);
        RequestClose();
    }

    /// <summary>「稍后提醒」：记住这个版本 + 24 小时窗口，到期后重新提示。</summary>
    private void Snooze()
    {
        var state = UpdateState.Load();
        state.SnoozedVersion = _info.Version.ToString();
        state.SnoozedUntilUtc = DateTime.UtcNow.Add(UpdateState.SnoozeDuration);
        state.Save();
        UpdateService.Log($"用户选择稍后提醒 {state.SnoozedVersion}，{state.SnoozedUntilUtc:u} 之前不再提示");
        RequestClose();
    }

    private void OpenReleasePage()
    {
        if (_info.ReleasePageUri is null)
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_info.ReleasePageUri.ToString())
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            UpdateService.Log($"打开 Release 页面失败：{ex.Message}");
        }
    }

    private async Task InstallAsync()
    {
        if (IsBusy)
        {
            return;
        }

        _cancellation = new CancellationTokenSource();
        IsBusy = true;
        OnPropertyChanged(nameof(CanStartInstall));
        OnPropertyChanged(nameof(CanSkip));
        HasFailed = false;
        IsProgressVisible = true;

        try
        {
            var progress = new Progress<double>(ratio =>
            {
                ProgressValue = Math.Clamp(ratio * 100, 0, 100);
                StatusText = string.Format(
                    _localization.GetString("Update_Downloading"),
                    ((int)ProgressValue).ToString(CultureInfo.InvariantCulture));
            });

            var installerPath = await _updates.DownloadAsync(_info, progress, _cancellation.Token);

            // DownloadAsync 内部已完成 SHA256 校验，这里只做状态呈现
            StatusText = _localization.GetString("Update_Verifying");
            ProgressValue = 100;

            StatusText = _localization.GetString("Update_Preparing");

            var backupDirectory = _updates.BackupUserData();
            if (backupDirectory is null)
            {
                UpdateService.Log("更新前未能备份用户数据（可能尚无数据文件）");
            }

            if (!_updates.LaunchInstaller(installerPath))
            {
                throw new InvalidOperationException("无法启动安装程序，请稍后重试或手动下载安装包。");
            }

            // 给安装器约 1 秒完成进程创建与日志初始化，再退出自身释放文件占用
            await Task.Delay(900);

            _shutdown();
        }
        catch (OperationCanceledException)
        {
            // 正常路径下不会被触发（下载中禁止关闭对话框），保留分支是为了让取消后状态可恢复
            HasFailed = true;
            IsProgressVisible = false;
            StatusText = _localization.GetString("Update_InstallNow");
        }
        catch (Exception ex)
        {
            UpdateService.Log($"更新失败：{ex}");
            HasFailed = true;
            IsProgressVisible = false;
            StatusText = string.Format(_localization.GetString("Update_Failed"), ex.Message);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanStartInstall));
            OnPropertyChanged(nameof(CanSkip));
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }
}
