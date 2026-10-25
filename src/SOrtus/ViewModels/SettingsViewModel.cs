using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using SOrtus.Common;
using SOrtus.Core.Services;
using SOrtus.Repositories;
using SOrtus.Services;
using SOrtus.Updates;

namespace SOrtus.ViewModels;

/// <summary>设置项选项（值 + 本地化显示名）。</summary>
internal sealed record SettingsOption(string Value, string Display);

/// <summary>
/// 设置视图模型（规范 4.4 / 7）：主题切换、高斯模糊强度调节（滑块实时预览）、语言切换。
/// 修改实时生效并持久化（ThemeService / LocalizationService 负责）。
/// </summary>
internal sealed class SettingsViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly ILocalizationService _localization;
    private readonly UpdatesViewModel _updates;

    private bool _autoCheckForUpdates;
    private bool _showFocusRing;
    private string _updateStatusText = string.Empty;
    private DispatcherTimer? _blurCommitTimer;

    public SettingsViewModel(IThemeService themeService, ILocalizationService localization, UpdatesViewModel? updates = null)
    {
        _themeService = themeService;
        _localization = localization;
        _updates = updates ?? new UpdatesViewModel(new DialogService(localization), localization);

        var settings = AppSettings.Load();
        _autoCheckForUpdates = settings.AutoCheckForUpdates;
        _showFocusRing = settings.ShowFocusRing;

        OpenDatabaseFolderCommand = new RelayCommand(_ => OpenDatabaseFolder());
        CopyDatabasePathCommand = new RelayCommand(_ => CopyDatabasePath());
        CheckUpdatesCommand = new AsyncRelayCommand(_ => CheckUpdatesAsync(), _ => !_updates.IsChecking);
        ToggleAutoCheckCommand = new RelayCommand(_ => AutoCheckForUpdates = !AutoCheckForUpdates);

        RebuildOptions();
    }

    /// <summary>主题 / 语言下拉项。语言切换后需要重建（VD-110），因此用可观察集合。</summary>
    public ObservableCollection<SettingsOption> ThemeOptions { get; } = new();

    public ObservableCollection<SettingsOption> LanguageOptions { get; } = new();

    private void RebuildOptions()
    {
        ThemeOptions.Clear();
        ThemeOptions.Add(new SettingsOption("Light", _localization.GetString("Settings_ThemeLight")));
        ThemeOptions.Add(new SettingsOption("Dark", _localization.GetString("Settings_ThemeDark")));

        LanguageOptions.Clear();
        LanguageOptions.Add(new SettingsOption("zh-CN", _localization.GetString("Settings_LanguageZh")));
        LanguageOptions.Add(new SettingsOption("en-US", _localization.GetString("Settings_LanguageEn")));
    }

    /// <summary>
    /// 语言切换后就地刷新视图模型自己拼好的文案（VD-110）。
    /// XAML 侧 DynamicResource 会自动变，但这里 format 过的字符串不会——不刷新就会出现
    /// 「Settings（新）」+「共 3 个立项（旧）」这种半中半英。
    /// </summary>
    public override void OnCultureChanged()
    {
        base.OnCultureChanged();
        RebuildOptions();

        // 下拉项与提示都是构造/取值时定格的，重建集合后要再通知一次
        OnPropertyChanged(nameof(ThemeOptions));
        OnPropertyChanged(nameof(LanguageOptions));

        // 手动检查的结果句是旧语言拼的，直接清掉，让用户重新点一次，而不是留一句半中半英
        UpdateStatusText = string.Empty;
    }

    /// <summary>
    /// 未提供语言包的提示（VD-013：单一资源句，不再拼装出「P3 版本提供」的版本计划暴露）。
    /// </summary>
    public string LanguageSoonHint => _localization.GetString("Settings_LanguageSoonHint");

    /// <summary>当前实际使用的数据库文件路径（VD-012：展示真实路径，替代 %LocalAppData% 占位符）。</summary>
    public string DatabasePath => Database.CurrentDbPath;

    /// <summary>数据库位置展示文本（resx：Settings_DbPath = 数据库位置：{0}）。</summary>
    public string DatabasePathText => string.Format(
        _localization.GetString("Settings_DbPath"), DatabasePath);

    /// <summary>打开数据库所在文件夹（资源管理器中定位，便于用户备份）。</summary>
    public ICommand OpenDatabaseFolderCommand { get; }

    /// <summary>复制数据库完整路径（VD-112：路径改单行省略后，需要另一条途径拿到全路径）。</summary>
    public ICommand CopyDatabasePathCommand { get; }

    /// <summary>当前运行版本（软件更新区域展示）。</summary>
    public string CurrentVersionText => _updates.CurrentVersionText;

    /// <summary>
    /// 自动检查更新开关。关闭后程序不再在启动时联网，
    /// 但「立即检查」按钮始终可用——用户只是不想被自动打扰，不是不想更新。
    /// </summary>
    public bool AutoCheckForUpdates
    {
        get => _autoCheckForUpdates;
        set
        {
            if (_autoCheckForUpdates == value)
            {
                return;
            }

            _autoCheckForUpdates = value;
            var settings = AppSettings.Load();
            settings.AutoCheckForUpdates = value;
            settings.Save();
            OnPropertyChanged();
            OnPropertyChanged(nameof(AutoCheckStateText));
        }
    }

    /// <summary>
    /// VD-108：状态由开关本身表达（轨道颜色 + 圆点位置），文案只作补充说明，
    /// 不再让按钮文字承担「当前是开还是关」——按钮表示动作，开关才表示状态。
    /// </summary>
    public string AutoCheckStateText => _autoCheckForUpdates
        ? _localization.GetString("Settings_AutoCheckOn")
        : _localization.GetString("Settings_AutoCheckOff");

    /// <summary>切换自动检查开关（保留给键盘快捷键与测试用，界面绑定的是 IsChecked）。</summary>
    public ICommand ToggleAutoCheckCommand { get; }

    /// <summary>
    /// 是否显示键盘焦点框（默认开）。
    /// 关闭后只去掉那圈额外的强调环，输入框描边与文本光标保留——键盘焦点仍然可辨。
    /// </summary>
    public bool ShowFocusRing
    {
        get => _showFocusRing;
        set
        {
            if (_showFocusRing == value)
            {
                return;
            }

            _showFocusRing = value;
            var settings = AppSettings.Load();
            settings.ShowFocusRing = value;
            settings.Save();
            FocusRingSettings.Apply(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(FocusRingStateText));
        }
    }

    /// <summary>焦点框开关的状态文案（与自动检查同一套「已开启 / 已关闭」用词）。</summary>
    public string FocusRingStateText => _showFocusRing
        ? _localization.GetString("Settings_AutoCheckOn")
        : _localization.GetString("Settings_AutoCheckOff");

    /// <summary>手动检查更新的执行状态（按钮禁用 + 文案切换）。</summary>
    public bool IsCheckingUpdates => _updates.IsChecking;

    /// <summary>手动检查的结果反馈（已是最新 / 失败原因）。</summary>
    public string UpdateStatusText
    {
        get => _updateStatusText;
        private set
        {
            if (SetProperty(ref _updateStatusText, value))
            {
                OnPropertyChanged(nameof(IsUpdateStatusVisible));
            }
        }
    }

    public bool IsUpdateStatusVisible => !string.IsNullOrEmpty(_updateStatusText);

    /// <summary>手动检查更新（关于页将来复用同一入口）。</summary>
    public ICommand CheckUpdatesCommand { get; }

    private async Task CheckUpdatesAsync()
    {
        UpdateStatusText = _localization.GetString("Update_Checking");
        OnPropertyChanged(nameof(IsCheckingUpdates));

        try
        {
            var result = await _updates.CheckAsync(force: true);

            switch (result.Status)
            {
                case UpdateCheckStatus.Available when result.Info is not null:
                    UpdateStatusText = string.Format(
                        _localization.GetString("Update_BannerText"), result.Info.Version);
                    await _updates.ShowDetailsAsync();
                    break;

                case UpdateCheckStatus.Failed:
                    UpdateStatusText = string.Format(
                        _localization.GetString("Update_CheckFailed"), result.Message);
                    break;

                default:
                    UpdateStatusText = string.Format(
                        _localization.GetString("Update_UpToDate"), CurrentVersionText);
                    break;
            }
        }
        finally
        {
            OnPropertyChanged(nameof(IsCheckingUpdates));
        }
    }

    private void OpenDatabaseFolder()
    {
        var directory = Path.GetDirectoryName(DatabasePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", directory)
        {
            UseShellExecute = true,
        });
    }

    private void CopyDatabasePath()
    {
        try
        {
            System.Windows.Clipboard.SetText(DatabasePath);
            UpdateStatusText = _localization.GetString("Settings_PathCopied");
        }
        catch (Exception ex)
        {
            // 剪贴板被其它进程占用时会抛 COM 异常；不弹窗，落在状态行里即可
            UpdateStatusText = ex.Message;
        }
    }

    public string CurrentTheme
    {
        get => _themeService.CurrentTheme;
        set
        {
            if (!string.Equals(value, _themeService.CurrentTheme, StringComparison.OrdinalIgnoreCase))
            {
                _themeService.SetTheme(value);
                OnPropertyChanged();
            }
        }
    }

    public string CurrentLanguage
    {
        get => _localization.CurrentCulture;
        set
        {
            if (!string.Equals(value, _localization.CurrentCulture, StringComparison.OrdinalIgnoreCase))
            {
                _localization.SetCulture(value);
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 模糊半径（滑块实时预览，0~30 整数）。
    /// <para>
    /// VD-109：拖动中只走 <see cref="IThemeService.PreviewBlurStrength"/>（内存 + 全局资源），
    /// 停手 400ms 才落盘一次。旧实现每 1px 写一次 settings.json，一次拖动就是几十次同步 IO。
    /// </para>
    /// </summary>
    public double BlurRadius
    {
        get => _themeService.GlobalBlurRadius;
        set
        {
            var snapped = Math.Round(value);
            if (!snapped.Equals(_themeService.GlobalBlurRadius))
            {
                _themeService.PreviewBlurStrength(snapped);
                OnPropertyChanged();
                OnPropertyChanged(nameof(BlurSummary));
                ScheduleBlurCommit();
            }
        }
    }

    private void ScheduleBlurCommit()
    {
        _blurCommitTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _blurCommitTimer.Stop();
        _blurCommitTimer.Tick -= OnBlurCommitTick;
        _blurCommitTimer.Tick += OnBlurCommitTick;
        _blurCommitTimer.Start();
    }

    private void OnBlurCommitTick(object? sender, EventArgs e)
    {
        _blurCommitTimer?.Stop();
        _themeService.CommitBlurStrength();
    }

    /// <summary>当前强度摘要（resx：Settings_BlurCurrent = 当前强度：{0}（{1}））。</summary>
    public string BlurSummary => string.Format(
        _localization.GetString("Settings_BlurCurrent"),
        ((int)Math.Round(_themeService.GlobalBlurRadius)).ToString(System.Globalization.CultureInfo.InvariantCulture),
        BlurStrengthName);

    private string BlurStrengthName => _themeService.GlobalBlurRadius switch
    {
        <= 0 => _localization.GetString("Settings_BlurNone"),
        < 9 => _localization.GetString("Settings_BlurSubtle"),
        < 18 => _localization.GetString("Settings_BlurNormal"),
        < 25 => _localization.GetString("Settings_BlurStrong"),
        _ => _localization.GetString("Settings_BlurOverlay"),
    };
}
