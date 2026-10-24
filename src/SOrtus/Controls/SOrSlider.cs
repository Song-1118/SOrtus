using System.Windows;
using System.Windows.Controls;

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

    static SOrSlider()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SOrSlider), new FrameworkPropertyMetadata(typeof(SOrSlider)));
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
