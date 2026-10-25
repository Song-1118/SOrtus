using System.Windows;
using System.Windows.Controls.Primitives;

namespace SOrtus.Controls;

/// <summary>
/// 开关（轨道 + 圆点）。
/// <para>
/// VD-108：设置页此前用普通按钮承载「已开启 / 已关闭」——按钮表示「执行一个动作」，
/// 开关才表示「保持一个状态」。用户每次都要先把文字读一遍、再推断点下去会变成什么，
/// 而同一页下面紧挨着的就是滑块与下拉框，唯独开关是按钮，控件语义不成体系。
/// </para>
/// <para>
/// 视觉：轨道 44×24 圆角 12，圆点 18，行程 20px，120ms 位移 + 轨道底色淡入；
/// 关闭态轨道用 BorderStrongBrush（与输入框边界同档，≥3:1 可见），开启态用 PrimaryBrush。
/// </para>
/// </summary>
public sealed class SOrToggleSwitch : ToggleButton
{
    /// <summary>圆角半径（缺省 12，即轨道高度的半径）。</summary>
    public static readonly DependencyProperty TrackCornerRadiusProperty =
        DependencyProperty.Register(
            nameof(TrackCornerRadius), typeof(CornerRadius), typeof(SOrToggleSwitch),
            new PropertyMetadata(new CornerRadius(12)));

    static SOrToggleSwitch()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SOrToggleSwitch),
            new FrameworkPropertyMetadata(typeof(SOrToggleSwitch)));
    }

    public CornerRadius TrackCornerRadius
    {
        get => (CornerRadius)GetValue(TrackCornerRadiusProperty);
        set => SetValue(TrackCornerRadiusProperty, value);
    }
}
