using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using SOrtus.Core.Services;

namespace SOrtus.Services;

/// <summary>
/// 主题与高斯模糊强度服务实现（规范 7 / 4.4）。
/// - 主题切换：运行时整本替换 Application.Resources 中的主题字典（Light/Dark 资源键一一对应）。
/// - 模糊强度：修改全局资源 GlobalBlurRadius，所有 BlurPanel 经 DynamicResource 自动响应。
/// <para>
/// VD-061（P0）：主题字典换掉之后，必须把所有画刷引用重新求值一次。
/// 只换字典是不够的——元素属性上挂的是「指向 SurfaceBrush 这个键」的资源引用，
/// 键本身没变，WPF 不会主动重新解析；而画刷实例在资源字典里会被冻结，
/// 冻结后其内部的 Color 表达式也不再跟随主题。于是出现「深色主题 + 白卡 + 黑字」。
/// 修法：替换字典后遍历全部窗口的视觉树，对承载颜色的依赖属性逐个 InvalidateProperty，
/// 迫使它们重新走一次资源查找（画刷已全部改为 x:Shared="False"，每次查找都是新实例）。
/// </para>
/// </summary>
internal sealed class ThemeService : IThemeService
{
    /// <summary>
    /// 主题切换后需要重新求值的依赖属性。只覆盖「承载画刷」的那几类，
    /// 不遍历全部属性——逐元素全量刷新会明显拖慢切换。
    /// </summary>
    private static readonly DependencyProperty[] BrushProperties =
    {
        Panel.BackgroundProperty,
        Control.BackgroundProperty,
        Control.ForegroundProperty,
        Control.BorderBrushProperty,
        TextBlock.BackgroundProperty,
        TextBlock.ForegroundProperty,
        Border.BackgroundProperty,
        Border.BorderBrushProperty,
        Shape.FillProperty,
        Shape.StrokeProperty,
        TextElement.ForegroundProperty,
        TextElement.BackgroundProperty,
    };

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

        RefreshBrushReferences();
    }

    private static void ApplyBlurRadius(double radius)
    {
        var app = Application.Current;
        if (app is not null)
        {
            app.Resources["GlobalBlurRadius"] = radius;
        }
    }

    /// <summary>遍历全部窗口的视觉树，重挂画刷类属性的资源引用（VD-061）。</summary>
    private static void RefreshBrushReferences()
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        foreach (Window window in app.Windows)
        {
            InvalidateBrushes(window);
        }
    }

    private static void InvalidateBrushes(DependencyObject node)
    {
        foreach (var property in BrushProperties)
        {
            try
            {
                node.InvalidateProperty(property);
            }
            catch (InvalidOperationException)
            {
                // 某些属性在特定元素上没有生效值来源，忽略即可
            }
        }

        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
        {
            InvalidateBrushes(VisualTreeHelper.GetChild(node, i));
        }
    }
}
