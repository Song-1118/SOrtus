using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SOrtus.Controls;

/// <summary>
/// 自定义进度条（MVP 控件库）：细轨 + 圆角填充，数值变化带 220ms 缓动（PRD 6.1 平滑动画）。
/// <para>命名层：<c>PART_Root</c>（轨道容器）、<c>PART_Fill</c>（填充）。动画只改 Width，不触碰任何模糊（规范 7.7）。</para>
/// </summary>
public sealed class SOrProgressBar : RangeBase
{
    /// <summary>填充画刷（缺省取主题 PrimaryBrush）。</summary>
    public static readonly DependencyProperty BarBrushProperty =
        DependencyProperty.Register(
            nameof(BarBrush), typeof(Brush), typeof(SOrProgressBar),
            new PropertyMetadata(null, OnBarBrushChanged));

    /// <summary>轨道画刷（缺省取主题 BorderBrush）。</summary>
    public static readonly DependencyProperty TrackBrushProperty =
        DependencyProperty.Register(
            nameof(TrackBrush), typeof(Brush), typeof(SOrProgressBar),
            new PropertyMetadata(null, OnTrackBrushChanged));

    /// <summary>圆角半径（缺省 3：圆角矩形，避免细条被渲染成胶囊）。</summary>
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius), typeof(CornerRadius), typeof(SOrProgressBar),
            new PropertyMetadata(new CornerRadius(3)));

    private FrameworkElement? _root;
    private Border? _fill;

    static SOrProgressBar()
    {
        MinimumProperty.OverrideMetadata(typeof(SOrProgressBar), new FrameworkPropertyMetadata(0.0));
        MaximumProperty.OverrideMetadata(typeof(SOrProgressBar), new FrameworkPropertyMetadata(100.0));
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SOrProgressBar), new FrameworkPropertyMetadata(typeof(SOrProgressBar)));
    }

    public Brush? BarBrush
    {
        get => (Brush?)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    public Brush? TrackBrush
    {
        get => (Brush?)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _root = GetTemplateChild("PART_Root") as FrameworkElement;
        _fill = GetTemplateChild("PART_Fill") as Border;

        if (_root is not null)
        {
            _root.SizeChanged -= OnRootSizeChanged;
            _root.SizeChanged += OnRootSizeChanged;
        }

        ApplyBrushes();
        UpdateFill(animate: false);
    }

    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        UpdateFill(animate: true);
    }

    private static void OnBarBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((SOrProgressBar)d).ApplyBrushes();

    private static void OnTrackBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((SOrProgressBar)d).ApplyBrushes();

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e) => UpdateFill(animate: false);

    private void ApplyBrushes()
    {
        if (_fill is not null)
        {
            _fill.Background = BarBrush ?? ResolveBrush("PrimaryBrush");
        }

        if (_root is Border border)
        {
            border.Background = TrackBrush ?? ResolveBrush("BorderBrush");
        }
    }

    private void UpdateFill(bool animate)
    {
        if (_fill is null || _root is null)
        {
            return;
        }

        var range = Maximum - Minimum;
        var ratio = range <= 0 ? 0 : (Value - Minimum) / range;
        var width = Math.Max(0, _root.ActualWidth) * Math.Clamp(ratio, 0, 1);

        if (animate && _fill.ActualWidth > 0 && !width.Equals(_fill.ActualWidth))
        {
            var animation = new DoubleAnimation(width, new Duration(System.TimeSpan.FromMilliseconds(220)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            _fill.BeginAnimation(Border.WidthProperty, animation);
        }
        else
        {
            _fill.BeginAnimation(Border.WidthProperty, null);
            _fill.Width = width;
        }
    }

    private static Brush ResolveBrush(string key) =>
        Application.Current?.TryFindResource(key) as Brush ?? Brushes.Transparent;
}
