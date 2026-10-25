using System.Windows;
using System.Windows.Controls;

namespace SOrtus.Controls;

/// <summary>
/// 加载指示器（旋转弧线）。
/// <para>
/// 规范负面清单第 7 条：除加载指示外，任何「呼吸、闪烁、漂浮」在桌面工具里都是噪声——
/// 所以这是全站唯一允许的循环动画，且只在真正有异步操作在跑时出现。
/// </para>
/// <para>
/// 视觉树命名层：<c>PART_Arc</c>（旋转的亮弧）。旋转动画在
/// <c>Resources/Styles/Controls.Display.xaml</c> 的模板里接线，动画资源本身在
/// <c>Animations.xaml</c>（规范要求动画资源集中）。
/// </para>
/// </summary>
public sealed class SOrSpinner : Control
{
    /// <summary>直径（像素）。</summary>
    public static readonly DependencyProperty SizeProperty =
        DependencyProperty.Register(
            nameof(Size), typeof(double), typeof(SOrSpinner),
            new FrameworkPropertyMetadata(16.0));

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    static SOrSpinner()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SOrSpinner),
            new FrameworkPropertyMetadata(typeof(SOrSpinner)));
    }
}
