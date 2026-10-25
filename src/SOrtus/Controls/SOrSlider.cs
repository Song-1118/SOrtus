using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SOrtus.Controls;

/// <summary>
/// 自定义滑块（MVP 控件库）：4px 细轨 + 圆点滑块，用于进度与模糊强度调节。
/// <para>命名层：<c>PART_Root</c>、<c>PART_Track</c>（原生约定，承载填充 / 滑块）。</para>
/// </summary>
public sealed class SOrSlider : Slider
{
    /// <summary>圆角半径（缺省 3：圆角矩形，避免细轨端头呈椭圆）。</summary>
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius), typeof(CornerRadius), typeof(SOrSlider),
            new PropertyMetadata(new CornerRadius(3)));

    /// <summary>
    /// 点击轨道是否「动画滑到点击处」（用户反馈：点一下动一下的步进感很别扭）。
    /// <para>
    /// 实现只动拖块的视觉位置（RenderTransform 偏移动画），<b>不逐帧改 Value</b>——
    /// 模糊强度滑块绑的是实时预览，逐帧改值会逐帧重建模糊缓存（规范负面清单第 1 条：
    /// 不要给模糊半径做动画）。动画结束时才提交一次目标值。
    /// </para>
    /// </summary>
    public static readonly DependencyProperty IsAnimatedTrackClickProperty =
        DependencyProperty.Register(
            nameof(IsAnimatedTrackClick), typeof(bool), typeof(SOrSlider),
            new PropertyMetadata(true));

    public bool IsAnimatedTrackClick
    {
        get => (bool)GetValue(IsAnimatedTrackClickProperty);
        set => SetValue(IsAnimatedTrackClickProperty, value);
    }

    static SOrSlider()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SOrSlider), new FrameworkPropertyMetadata(typeof(SOrSlider)));
    }

    /// <summary>
    /// 轨道点击必须在**预览（隧道）阶段**拦截：若在冒泡阶段处理，Track 里的 RepeatButton
    /// 已经先一步执行了 <c>Slider.DecreaseLarge</c> / <c>IncreaseLarge</c>（那正是「点一下动一下」，
    /// 表现为偶尔只减少/增加 1）。隧道阶段先于所有冒泡处理者，Handled=true 能真正掐断步进命令。
    /// </summary>
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        // 拖块本体：交给默认的拖动逻辑
        if (IsOnThumb(e.OriginalSource as DependencyObject))
        {
            base.OnPreviewMouseLeftButtonDown(e);
            return;
        }

        if (!IsAnimatedTrackClick || !TryGetTrackTarget(e, out var target))
        {
            base.OnPreviewMouseLeftButtonDown(e);
            return;
        }

        e.Handled = true;
        AnimateTo(target);
    }

    /// <summary>命中点是否落在拖块上。</summary>
    private bool IsOnThumb(DependencyObject? source)
    {
        var thumb = (Template?.FindName("PART_Track", this) as Track)?.Thumb;
        if (thumb is null || source is null)
        {
            return false;
        }

        for (var current = VisualTreeHelper.GetParent(source); current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, thumb))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>按点击位置算出目标值（按拖块中心对齐，与浏览器滑轨一致）。</summary>
    private bool TryGetTrackTarget(MouseButtonEventArgs e, out double target)
    {
        target = Value;
        var track = Template?.FindName("PART_Track", this) as Track;
        if (track is null)
        {
            return false;
        }

        var point = e.GetPosition(track);
        double trackWidth = track.ActualWidth;
        if (trackWidth <= 0)
        {
            return false;
        }

        double thumbWidth = track.Thumb?.ActualWidth ?? 0;
        double usable = Math.Max(1, trackWidth - thumbWidth);
        double ratio = Math.Clamp((point.X - thumbWidth / 2) / usable, 0, 1);

        target = Minimum + ratio * (Maximum - Minimum);
        if (IsSnapToInteger)
        {
            target = Math.Round(target);
        }

        return true;
    }

    /// <summary>
    /// 动画滑到目标值：直接动画 <see cref="RangeBase.ValueProperty"/>，
    /// 这样拖块与已填充段同步推进（此前只动画视觉偏移，填充段原地不动，看上去像「原点瞬移、进度条滞后」）。
    /// <para>
    /// 绑定实时预览的场景（如模糊强度）会逐帧收到值——由 <c>ThemeService.PreviewBlurStrength</c>
    /// 的节流兜底，避免逐帧重建模糊缓存（规范负面清单第 1 条）。
    /// </para>
    /// </summary>
    private void AnimateTo(double target)
    {
        if (Math.Abs(Value - target) < 0.001)
        {
            return;
        }

        var animation = new DoubleAnimation
        {
            From = Value,
            To = target,
            Duration = new Duration(TimeSpan.FromMilliseconds(240)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        BeginAnimation(ValueProperty, animation);
    }

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>整数值变化（模糊强度等整数场景使用，避免浮点拖拽积累）。</summary>
    public static readonly DependencyProperty IsSnapToIntegerProperty =
        DependencyProperty.Register(
            nameof(IsSnapToInteger), typeof(bool), typeof(SOrSlider),
            new PropertyMetadata(false));

    public bool IsSnapToInteger
    {
        get => (bool)GetValue(IsSnapToIntegerProperty);
        set => SetValue(IsSnapToIntegerProperty, value);
    }

    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        if (IsSnapToInteger)
        {
            var snapped = Math.Round(newValue);
            if (!snapped.Equals(newValue))
            {
                SetCurrentValue(ValueProperty, snapped);
            }
        }
    }
}
