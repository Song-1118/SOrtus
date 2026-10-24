using System.Collections.Generic;
using System.Windows.Input;
using SOrtus.Common;
using SOrtus.Core.Services;

namespace SOrtus.ViewModels;

/// <summary>设置项选项（值 + 本地化显示名）。</summary>
internal sealed record SettingsOption(string Value, string Display);

/// <summary>
/// 设置视图模型（规范 4.4 / 7）：主题切换、高斯模糊强度调节（下拉档位 + 滑块实时预览）、语言切换。
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

        ThemeOptions = new List<SettingsOption>
        {
            new("Light", localization.GetString("Settings_ThemeLight")),
            new("Dark", localization.GetString("Settings_ThemeDark")),
        };

        BlurOptions = new List<SettingsOption>
        {
            new("0", localization.GetString("Settings_BlurNone")),
            new("6", localization.GetString("Settings_BlurSubtle")),
            new("12", localization.GetString("Settings_BlurNormal")),
            new("20", localization.GetString("Settings_BlurStrong")),
            new("30", localization.GetString("Settings_BlurOverlay")),
        };

        LanguageOptions = new List<SettingsOption>
        {
            new("zh-CN", localization.GetString("Settings_LanguageZh")),
            new("en-US", localization.GetString("Settings_LanguageEn")),
        };
    }

    public List<SettingsOption> ThemeOptions { get; }
    public List<SettingsOption> BlurOptions { get; }
    public List<SettingsOption> LanguageOptions { get; }

    /// <summary>未提供语言包的提示（韩 / 日在后续版本）。</summary>
    public string LanguageSoonHint => string.Join(" / ",
        _localization.GetString("Settings_LanguageKo"),
        _localization.GetString("Settings_LanguageJa"))
        + " · " + _localization.GetString("List_TypeSoon");

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

    /// <summary>模糊档位（下拉，值为半径）。</summary>
    public string SelectedBlurValue
    {
        get => ((int)Math.Round(_themeService.GlobalBlurRadius)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        set
        {
            if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var radius))
            {
                _themeService.SetBlurStrength(radius);
                OnPropertyChanged();
                OnPropertyChanged(nameof(BlurRadius));
                OnPropertyChanged(nameof(BlurSummary));
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
                OnPropertyChanged(nameof(SelectedBlurValue));
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
