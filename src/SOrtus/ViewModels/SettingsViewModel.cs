using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using SOrtus.Common;
using SOrtus.Core.Services;
using SOrtus.Repositories;

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

    public SettingsViewModel(IThemeService themeService, ILocalizationService localization)
    {
        _themeService = themeService;
        _localization = localization;

        OpenDatabaseFolderCommand = new RelayCommand(_ => OpenDatabaseFolder());

        ThemeOptions = new List<SettingsOption>
        {
            new("Light", localization.GetString("Settings_ThemeLight")),
            new("Dark", localization.GetString("Settings_ThemeDark")),
        };

        LanguageOptions = new List<SettingsOption>
        {
            new("zh-CN", localization.GetString("Settings_LanguageZh")),
            new("en-US", localization.GetString("Settings_LanguageEn")),
        };
    }

    public List<SettingsOption> ThemeOptions { get; }
    public List<SettingsOption> LanguageOptions { get; }

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

    /// <summary>模糊半径（滑块实时预览，0~30 整数）。</summary>
    public double BlurRadius
    {
        get => _themeService.GlobalBlurRadius;
        set
        {
            var snapped = Math.Round(value);
            if (!snapped.Equals(_themeService.GlobalBlurRadius))
            {
                _themeService.SetBlurStrength(snapped);
                OnPropertyChanged();
                OnPropertyChanged(nameof(BlurSummary));
            }
        }
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
