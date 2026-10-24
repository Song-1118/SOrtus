using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace SOrtus.Controls;

/// <summary>
/// 高斯模糊面板附加组件（规范 7.4.1）。
/// <para>
/// 为任意 <see cref="FrameworkElement"/> 提供「背景模糊层（VisualBrush + BlurEffect）+ 覆盖层」双层结构，
/// 使用 Grid 承载，保证模糊只作用于背景、不模糊文字与子控件。模糊层随控件尺寸自动跟随（Loaded / SizeChanged）。
/// 模糊半径、覆盖层色值通过资源字典提供，主题切换（DynamicResource）时自动响应。
/// </para>
/// <para>性能：静态模糊层启用 <see cref="BitmapCache"/>；禁止逐帧重算模糊半径（规范 7.7）。</para>
/// <para>约定：页面引用此组件后不得再出现内联 &lt;BlurEffect&gt;（规范 7.9 A14/A15）。</para>
/// </summary>
public static class BlurPanel
{
    /// <summary>是否启用背景模糊。</summary>
    public static readonly DependencyProperty EnableBlurProperty =
        DependencyProperty.RegisterAttached(
            "EnableBlur", typeof(bool), typeof(BlurPanel),
            new PropertyMetadata(false, OnEnableBlurChanged));

    /// <summary>模糊半径（对应 Sizes.xaml 的命名档位）。</summary>
    public static readonly DependencyProperty BlurRadiusProperty =
        DependencyProperty.RegisterAttached(
            "BlurRadius", typeof(double), typeof(BlurPanel),
            new PropertyMetadata(12.0, OnBlurRadiusChanged));

    /// <summary>覆盖层透明度。</summary>
    public static readonly DependencyProperty OverlayOpacityProperty =
        DependencyProperty.RegisterAttached(
            "OverlayOpacity", typeof(double), typeof(BlurPanel),
            new PropertyMetadata(0.5, OnOverlayOpacityChanged));

    /// <summary>模糊层画刷；缺省取主题 WallpaperBrush（应用背景装饰）。</summary>
    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.RegisterAttached(
            "Background", typeof(Brush), typeof(BlurPanel),
            new PropertyMetadata(null, OnBackgroundChanged));

    /// <summary>
    /// 模糊层圆角半径（默认 0）。宿主控件自身是圆角时（如卡片），必须把同一半径传进来，
    /// 否则模糊层与覆盖层是直角矩形，会在圆角卡片上露出方角。
    /// </summary>
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.RegisterAttached(
            "CornerRadius", typeof(CornerRadius), typeof(BlurPanel),
            new PropertyMetadata(default(CornerRadius), OnCornerRadiusChanged));

    // 内部存储：承载层 Grid 与两个矩形
    private static readonly DependencyProperty LayerGridProperty =
        DependencyProperty.RegisterAttached("LayerGrid", typeof(Grid), typeof(BlurPanel), new PropertyMetadata(null));

    private static readonly DependencyProperty BlurRectProperty =
        DependencyProperty.RegisterAttached("BlurRect", typeof(Rectangle), typeof(BlurPanel), new PropertyMetadata(null));

    private static readonly DependencyProperty OverlayRectProperty =
        DependencyProperty.RegisterAttached("OverlayRect", typeof(Rectangle), typeof(BlurPanel), new PropertyMetadata(null));

    /// <summary>背景采样画刷（视觉源为窗口背景层时，按控件位置实时对齐取样区域）。</summary>
    private static readonly DependencyProperty BackdropBrushProperty =
        DependencyProperty.RegisterAttached("BackdropBrush", typeof(VisualBrush), typeof(BlurPanel), new PropertyMetadata(null));

    /// <summary>窗口背景层的命名约定（MainWindow.xaml 中的 PART_Backdrop）。</summary>
    private const string BackdropElementName = "PART_Backdrop";

    #region 附加属性读写

    public static bool GetEnableBlur(DependencyObject obj) => (bool)obj.GetValue(EnableBlurProperty);
    public static void SetEnableBlur(DependencyObject obj, bool value) => obj.SetValue(EnableBlurProperty, value);

    public static double GetBlurRadius(DependencyObject obj) => (double)obj.GetValue(BlurRadiusProperty);
    public static void SetBlurRadius(DependencyObject obj, double value) => obj.SetValue(BlurRadiusProperty, value);

    public static double GetOverlayOpacity(DependencyObject obj) => (double)obj.GetValue(OverlayOpacityProperty);
    public static void SetOverlayOpacity(DependencyObject obj, double value) => obj.SetValue(OverlayOpacityProperty, value);

    public static Brush? GetBackground(DependencyObject obj) => (Brush?)obj.GetValue(BackgroundProperty);
    public static void SetBackground(DependencyObject obj, Brush? value) => obj.SetValue(BackgroundProperty, value);

    public static CornerRadius GetCornerRadius(DependencyObject obj) => (CornerRadius)obj.GetValue(CornerRadiusProperty);
    public static void SetCornerRadius(DependencyObject obj, CornerRadius value) => obj.SetValue(CornerRadiusProperty, value);

    private static Grid? GetLayerGrid(DependencyObject obj) => (Grid?)obj.GetValue(LayerGridProperty);
    private static void SetLayerGrid(DependencyObject obj, Grid? value) => obj.SetValue(LayerGridProperty, value);

    private static Rectangle? GetBlurRect(DependencyObject obj) => (Rectangle?)obj.GetValue(BlurRectProperty);
    private static void SetBlurRect(DependencyObject obj, Rectangle? value) => obj.SetValue(BlurRectProperty, value);

    private static Rectangle? GetOverlayRect(DependencyObject obj) => (Rectangle?)obj.GetValue(OverlayRectProperty);
    private static void SetOverlayRect(DependencyObject obj, Rectangle? value) => obj.SetValue(OverlayRectProperty, value);

    private static VisualBrush? GetBackdropBrush(DependencyObject obj) => (VisualBrush?)obj.GetValue(BackdropBrushProperty);
    private static void SetBackdropBrush(DependencyObject obj, VisualBrush? value) => obj.SetValue(BackdropBrushProperty, value);

    #endregion

    private static void OnEnableBlurChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            Attach(fe);
        }
        else
        {
            Detach(fe);
        }
    }

    private static void OnBlurRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var rect = GetBlurRect(d);
        if (rect?.Effect is BlurEffect effect)
        {
            effect.Radius = (double)e.NewValue;
        }
    }

    private static void OnOverlayOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var rect = GetOverlayRect(d);
        if (rect is not null)
        {
            rect.Opacity = (double)e.NewValue;
        }
    }

    private static void OnBackgroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe)
        {
            return;
        }

        var rect = GetBlurRect(fe);
        if (rect is not null)
        {
            rect.Fill = ResolveBackdropFill(fe);
        }
    }

    private static void OnCornerRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement fe)
        {
            ApplyCornerRadius(fe);
        }
    }

    /// <summary>把宿主圆角同步到两层矩形：Rectangle 只有单一 RadiusX/RadiusY，取四角最大值即可保证不露直角。</summary>
    private static void ApplyCornerRadius(FrameworkElement fe)
    {
        var radius = GetCornerRadius(fe);
        var max = Math.Max(Math.Max(radius.TopLeft, radius.TopRight), Math.Max(radius.BottomLeft, radius.BottomRight));

        if (GetBlurRect(fe) is { } blurRect)
        {
            blurRect.RadiusX = max;
            blurRect.RadiusY = max;
        }

        if (GetOverlayRect(fe) is { } overlay)
        {
            overlay.RadiusX = max;
            overlay.RadiusY = max;
        }
    }

    private static void Attach(FrameworkElement fe)
    {
        if (GetLayerGrid(fe) is not null)
        {
            return;
        }

        fe.Loaded += OnLoaded;
        if (fe.IsLoaded)
        {
            BuildLayers(fe);
        }
    }

    private static void Detach(FrameworkElement fe)
    {
        fe.Loaded -= OnLoaded;
        fe.SizeChanged -= OnSizeChanged;
        fe.LayoutUpdated -= OnLayoutUpdated;
        var grid = GetLayerGrid(fe);
        if (grid is null)
        {
            return;
        }

        RemoveFromParent(grid);
        SetLayerGrid(fe, null);
        SetBlurRect(fe, null);
        SetOverlayRect(fe, null);
        SetBackdropBrush(fe, null);
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            BuildLayers(fe);
        }
    }

    private static void BuildLayers(FrameworkElement fe)
    {
        if (GetLayerGrid(fe) is not null)
        {
            return;
        }

        var grid = new Grid
        {
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0),
        };

        var blurRect = new Rectangle
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            CacheMode = new BitmapCache(),
            Effect = new BlurEffect
            {
                Radius = GetBlurRadius(fe),
                KernelType = KernelType.Gaussian,
                RenderingBias = RenderingBias.Performance,
            },
            Fill = ResolveBackdropFill(fe),
        };

        var overlay = new Rectangle
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Fill = ResolveOverlayBrush(),
            Opacity = GetOverlayOpacity(fe),
        };

        grid.Children.Add(blurRect);
        grid.Children.Add(overlay);

        SetLayerGrid(fe, grid);
        SetBlurRect(fe, blurRect);
        SetOverlayRect(fe, overlay);
        ApplyCornerRadius(fe);

        InsertLayer(fe, grid);
        fe.SizeChanged += OnSizeChanged;
        fe.LayoutUpdated += OnLayoutUpdated;
        UpdateBackdropViewbox(fe);
    }

    /// <summary>
    /// 模糊层的取样画刷：显式指定 Background 时以其为准；
    /// 否则若窗口存在背景层（PART_Backdrop），按控件在背景中的实际位置 1:1 取样，
    /// 让磨砂卡片呈现「同一张背景被磨砂」而非放大缩略图；再否则退回主题壁纸画刷。
    /// </summary>
    private static Brush ResolveBackdropFill(FrameworkElement fe)
    {
        var explicitBackground = GetBackground(fe);
        if (explicitBackground is not null)
        {
            SetBackdropBrush(fe, null);
            return explicitBackground;
        }

        var backdrop = FindBackdrop(fe);
        if (backdrop is not null)
        {
            var brush = new VisualBrush(backdrop)
            {
                Stretch = Stretch.Fill,
                ViewboxUnits = BrushMappingMode.Absolute,
                ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                Viewport = new Rect(0, 0, 1, 1),
                Viewbox = ComputeViewbox(fe, backdrop),
            };

            SetBackdropBrush(fe, brush);
            return brush;
        }

        SetBackdropBrush(fe, null);
        return ResolveDefaultBackdrop();
    }

    private static FrameworkElement? FindBackdrop(FrameworkElement fe)
    {
        var window = Window.GetWindow(fe);
        return window?.FindName(BackdropElementName) as FrameworkElement;
    }

    private static Rect ComputeViewbox(FrameworkElement fe, FrameworkElement backdrop)
    {
        if (fe.ActualWidth <= 0 || fe.ActualHeight <= 0)
        {
            return new Rect(0, 0, 1, 1);
        }

        try
        {
            var origin = fe.TransformToVisual(backdrop).Transform(new Point(0, 0));
            return new Rect(origin.X, origin.Y, fe.ActualWidth, fe.ActualHeight);
        }
        catch (InvalidOperationException)
        {
            // 尚未挂载到同一视觉树时无法变换，等下一次 LayoutUpdated 再对齐
            return new Rect(0, 0, 1, 1);
        }
    }

    private static void UpdateBackdropViewbox(FrameworkElement fe)
    {
        if (GetBackdropBrush(fe) is not { Visual: FrameworkElement backdrop } brush)
        {
            return;
        }

        var viewbox = ComputeViewbox(fe, backdrop);
        if (viewbox.Width > 0 && viewbox != brush.Viewbox)
        {
            brush.Viewbox = viewbox;
        }
    }

    private static void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            UpdateBackdropViewbox(fe);
        }
    }

    private static void InsertLayer(FrameworkElement fe, Grid grid)
    {
        switch (fe)
        {
            case Panel panel:
                panel.Children.Insert(0, grid);
                if (panel is Grid g)
                {
                    Grid.SetRowSpan(grid, Math.Max(1, g.RowDefinitions.Count));
                    Grid.SetColumnSpan(grid, Math.Max(1, g.ColumnDefinitions.Count));
                }

                break;

            case Decorator decorator:
            {
                var original = decorator.Child;
                var host = new Grid();
                host.Children.Add(grid);
                if (original is not null)
                {
                    host.Children.Add(original);
                }

                decorator.Child = host;
                break;
            }

            case ContentControl cc:
            {
                var original = cc.Content as UIElement;
                var host = new Grid();
                host.Children.Add(grid);
                if (original is not null)
                {
                    host.Children.Add(original);
                }

                cc.Content = host;
                break;
            }
        }
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 模糊层与覆盖层均 Stretch 填充，尺寸变更无需重置结构；仅需重新对齐背景取样区域
        if (sender is FrameworkElement fe)
        {
            UpdateBackdropViewbox(fe);
        }
    }

    private static void RemoveFromParent(FrameworkElement element)
    {
        if (element.Parent is Panel panel)
        {
            panel.Children.Remove(element);
        }
        else if (element.Parent is Decorator decorator && decorator.Child == element)
        {
            // 还原：将 host Grid 的子元素（原始内容）取出，重新挂回 Decorator
            if (element is Grid host && host.Children.Count > 1)
            {
                var original = host.Children[host.Children.Count - 1];
                host.Children.Remove(original);
                decorator.Child = original;
            }
            else
            {
                decorator.Child = null;
            }
        }
    }

    private static Brush ResolveDefaultBackdrop()
    {
        return (Brush?)Application.Current?.TryFindResource("WallpaperBrush") ?? Brushes.Transparent;
    }

    private static Brush ResolveOverlayBrush()
    {
        return (Brush?)Application.Current?.TryFindResource("BlurOverlayBrush") ?? Brushes.Transparent;
    }
}
