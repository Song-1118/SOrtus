using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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

        RebuildBlurOptions();

        LanguageOptions = new List<SettingsOption>
        {
            new("zh-CN", localization.GetString("Settings_LanguageZh")),
            new("en-US", localization.GetString("Settings_LanguageEn")),
        };
    }

    public List<SettingsOption> ThemeOptions { get; }
    /// <summary>模糊档位（滑块拖到非档位值时会动态补一条「自定义（N）」，避免下拉显示空白 —— SO-003）。</summary>
    public ObservableCollection<SettingsOption> BlurOptions { get; } = new();

    private static readonly double[] BlurPresets = { 0, 6, 12, 20, 30 };

    private void RebuildBlurOptions()
    {
        var current = (int)Math.Round(_themeService.GlobalBlurRadius);
        BlurOptions.Clear();
        foreach (var preset in BlurPresets)
        {
            var value = (int)preset;
            BlurOptions.Add(new SettingsOption(
                value.ToString(CultureInfo.InvariantCulture),
                _localization.GetString(value switch
                {
                    0 => "Settings_BlurNone",
                    6 => "Settings_BlurSubtle",
                    12 => "Settings_BlurNormal",
                    20 => "Settings_BlurStrong",
                    _ => "Settings_BlurOverlay",
                })));
        }

        if (!BlurPresets.Any(p => Math.Abs(p - current) < 0.5))
        {
            BlurOptions.Add(new SettingsOption(
                current.ToString(CultureInfo.InvariantCulture),
                string.Format(_localization.GetString("Settings_BlurCustom"), current)));
        }
    }
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
                RebuildBlurOptions();
                OnPropertyChanged();
                OnPropertyChanged(nameof(BlurRadius));
                OnPropertyChanged(nameof(BlurSummary));
                OnPropertyChanged(nameof(SelectedBlurValue));
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
                RebuildBlurOptions();
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
