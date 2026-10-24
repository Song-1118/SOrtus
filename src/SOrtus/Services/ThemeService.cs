using System.Windows;
using SOrtus.Core.Services;

namespace SOrtus.Services;

/// <summary>
/// 主题与高斯模糊强度服务实现（规范 7 / 4.4）。
/// - 主题切换：运行时整本替换 Application.Resources 中的主题字典（Light/Dark 资源键一一对应）。
/// - 模糊强度：修改全局资源 GlobalBlurRadius，所有 BlurPanel 经 DynamicResource 自动响应。
/// </summary>
internal sealed class ThemeService : IThemeService
{
    private AppSettings _settings = new();
    private string _currentTheme = "Light";
    private double _blurRadius = 12.0;

    public string CurrentTheme => _currentTheme;

    public double GlobalBlurRadius => _blurRadius;

    public event EventHandler? ThemeChanged;

    public void Initialize()
    {
        _settings = AppSettings.Load();
        _currentTheme = _settings.Theme;

        // 未自定义强度时取档位令牌 BlurRadiusNormal：改 Sizes.xaml 一处，全应用同步（规范 7.3 / A16）
        _blurRadius = _settings.BlurRadius ?? ReadNamedLevel("BlurRadiusNormal");
        ApplyTheme(_currentTheme);
        ApplyBlurRadius(_blurRadius);
    }

    public void SetTheme(string themeName)
    {
        if (string.Equals(_currentTheme, themeName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _currentTheme = themeName;
        _settings.Theme = themeName;
        _settings.Save();
        ApplyTheme(themeName);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetBlurStrength(double radius)
    {
        _blurRadius = radius;
        _settings.BlurRadius = radius;
        _settings.Save();
        ApplyBlurRadius(radius);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>读取 Sizes.xaml 中的命名模糊档位；资源缺失或类型不符时回退 Normal 档（12）。</summary>
    private static double ReadNamedLevel(string key)
    {
        var value = Application.Current?.TryFindResource(key);
        return value is double radius ? radius : 12.0;
    }

    private static void ApplyTheme(string name)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var themeDict = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source is not null &&
                                 d.Source.OriginalString.EndsWith("Theme.xaml", StringComparison.OrdinalIgnoreCase));
        if (themeDict is null)
        {
            return;
        }

        var source = new Uri($"pack://application:,,,/SOrtus;component/Resources/Themes/{name}.xaml", UriKind.Absolute);
        if (themeDict.MergedDictionaries.Count == 0)
        {
            themeDict.MergedDictionaries.Add(new ResourceDictionary { Source = source });
        }
        else
        {
            themeDict.MergedDictionaries[0].Source = source;
        }
    }

    private static void ApplyBlurRadius(double radius)
    {
        var app = Application.Current;
        if (app is not null)
        {
            app.Resources["GlobalBlurRadius"] = radius;
        }
    }
}
