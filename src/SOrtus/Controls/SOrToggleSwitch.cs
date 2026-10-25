using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

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

    /// <summary>
    /// 开关切换动画在这里播，而不是写在模板的 Trigger 里。
    /// <para>
    /// 原因：模板 Trigger 里的 <c>Setter</c> 若整体替换 <c>PART_Thumb</c> 的
    /// <c>RenderTransform</c> 对象，Storyboard 里的目标路径
    /// <c>(UIElement.RenderTransform).(TranslateTransform.X)</c> 就会指向**被换掉的那个旧对象**，
    /// 动画静默失效——表现是「开关一点就跳，没有滑动」。
    /// 代码侧直接取模板里那个 <c>TranslateTransform</c> 实例改它的 X，动画才真正作用在可见元素上。
    /// </para>
    /// </summary>
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == IsCheckedProperty)
        {
            PlayToggleAnimation(e.NewValue is true);
        }
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // 初始就处于开启态（绑定初值）时没有状态变化，动画不会触发——这里补一次，
        // 保证圆点落在开启位置而不是停在关闭端。
        // 必须**替换整个 TranslateTransform 实例**而不能改现有实例的 X：WPF 动画系统
        // 会在动画结束后冻结目标 Freezable，改它的属性会抛
        // InvalidOperationException（「第二次进入有开关的页面直接崩溃」的根因）。
        if (IsChecked == true && Template?.FindName("PART_Thumb", this) is FrameworkElement thumb)
        {
            thumb.RenderTransform = new TranslateTransform(24, 0);
        }
    }

    private void PlayToggleAnimation(bool isOn)
    {
        if (Template?.FindName("PART_Thumb", this) is not FrameworkElement thumb)
        {
            return;
        }

        // 同样替换新实例：上一次动画可能已把旧实例冻结，动画对冻结对象也会抛异常
        var transform = new TranslateTransform(isOn ? 24 : 0, 0);
        thumb.RenderTransform = transform;

        // 直接对变换实例做动画，**不走 Storyboard.Begin(this)**：
        // 绑定激活等时机下模板 NameScope 可能尚未就绪，Storyboard 解析 TargetName
        // 会抛「无法在模板范围中找到 PART_Thumb」（实测正是这个栈）。
        transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation
        {
            To = isOn ? 24 : 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(120)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });

        // 开启态轨道底色同步过渡
        if (Template?.FindName("PART_TrackOn", this) is FrameworkElement trackOn)
        {
            trackOn.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                To = isOn ? 1 : 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(120)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
        }
    }

    public CornerRadius TrackCornerRadius
    {
        get => (CornerRadius)GetValue(TrackCornerRadiusProperty);
        set => SetValue(TrackCornerRadiusProperty, value);
    }
}
