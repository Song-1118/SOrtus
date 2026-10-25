using System.Windows.Input;
using SOrtus.Common;
using SOrtus.Core.Services;
using SOrtus.Services;
using SOrtus.Updates;

namespace SOrtus.ViewModels;

/// <summary>
/// 更新编排视图模型：负责「什么时候查、查到之后要不要打扰用户」，
/// 不负责下载与安装（那是 <see cref="UpdateViewModel"/> 的事）。
/// <para>
/// 关键行为约定：
/// 1) 自动检查默认开启，但受 24 小时节流与用户开关双重约束；
/// 2) 检查失败绝不弹窗、绝不阻塞启动，只写日志——用户没要求更新，就不该被网络故障打扰；
/// 3) 只有「真的发现更高版本」才显示顶部提示条，避免常驻噪声。
/// </para>
/// </summary>
internal sealed class UpdatesViewModel : ViewModelBase
{
    private readonly UpdateService _updates;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localization;

    private UpdateInfo? _pending;
    private bool _hasUpdate;
    private bool _isChecking;
    private string? _lastError;

    public UpdatesViewModel(
        IDialogService dialogService,
        ILocalizationService localization,
        UpdateService? updates = null)
    {
        _dialogService = dialogService;
        _localization = localization;
        _updates = updates ?? UpdateService.Instance;

        ShowCommand = new AsyncRelayCommand(_ => ShowDetailsAsync(), _ => _pending is not null);
    }

    /// <summary>顶部提示条是否可见。</summary>
    public bool HasUpdate
    {
        get => _hasUpdate;
        private set => SetProperty(ref _hasUpdate, value);
    }

    /// <summary>提示条文案（新版本 0.2.0 可用）。</summary>
    public string BannerText => _pending is null
        ? string.Empty
        : string.Format(_localization.GetString("Update_BannerText"), _pending.Version);

    public string BannerActionText => _localization.GetString("Update_BannerAction");

    /// <summary>当前运行版本（设置页展示用）。</summary>
    public string CurrentVersionText => _updates.CurrentVersion.ToString();

    public bool IsChecking
    {
        get => _isChecking;
        private set => SetProperty(ref _isChecking, value);
    }

    /// <summary>最近一次检查失败原因（设置页可展示；null 表示无错误）。</summary>
    public string? LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }

    public ICommand ShowCommand { get; }

    /// <summary>
    /// 检查更新。
    /// </summary>
    /// <param name="force">true = 用户手动点击「立即检查」，忽略 24 小时节流。</param>
    public async Task<UpdateCheckResult> CheckAsync(bool force)
    {
        if (IsChecking)
        {
            return new UpdateCheckResult(UpdateCheckStatus.Throttled, _pending, null);
        }

        // 自动检查受用户开关约束；手动检查不受约束（用户点了就是要查）
        if (!force && !AppSettings.Load().AutoCheckForUpdates)
        {
            UpdateService.Log("自动检查更新已在设置中关闭，跳过本次检查");
            return new UpdateCheckResult(UpdateCheckStatus.Throttled, null, null);
        }

        IsChecking = true;
        try
        {
            var result = await _updates.CheckAsync(force);

            switch (result.Status)
            {
                case UpdateCheckStatus.Available:
                    _pending = result.Info;
                    HasUpdate = true;
                    LastError = null;
                    OnPropertyChanged(nameof(BannerText));
                    break;

                case UpdateCheckStatus.UpToDate:
                case UpdateCheckStatus.Skipped:
                case UpdateCheckStatus.Throttled:
                    _pending = result.Status == UpdateCheckStatus.Skipped ? result.Info : null;
                    HasUpdate = false;
                    LastError = null;
                    OnPropertyChanged(nameof(BannerText));
                    break;

                case UpdateCheckStatus.Failed:
                    HasUpdate = false;
                    LastError = result.Message;
                    break;
            }

            return result;
        }
        finally
        {
            IsChecking = false;
        }
    }

    /// <summary>打开更新详情覆盖层，任务在对话框关闭后完成。</summary>
    public async Task ShowDetailsAsync()
    {
        if (_pending is null)
        {
            return;
        }

        var dialog = new UpdateViewModel(_pending, _updates, _localization);

        // 用户在选择「跳过此版本」后关闭对话框，提示条要同步消失
        dialog.Closed += () =>
        {
            var state = UpdateState.Load();
            if (!string.IsNullOrEmpty(state.SkippedVersion)
                && SemanticVersion.TryParse(state.SkippedVersion, out var skipped)
                && skipped >= _pending.Version)
            {
                HasUpdate = false;
            }
        };

        await _dialogService.ShowAsync(dialog);
    }
}
