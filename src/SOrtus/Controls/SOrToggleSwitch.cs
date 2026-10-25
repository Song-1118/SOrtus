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
        if (IsChecked == true && Template?.FindName("PART_Thumb", this) is FrameworkElement thumb)
        {
            if (thumb.RenderTransform is TranslateTransform transform)
            {
                transform.X = 24;
            }
        }
    }

    private void PlayToggleAnimation(bool isOn)
    {
        if (Template?.FindName("PART_Thumb", this) is not FrameworkElement thumb)
        {
            return;
        }

        var key = isOn ? "ToggleOnStoryboard" : "ToggleOffStoryboard";
        if (Application.Current?.TryFindResource(key) is not Storyboard storyboard)
        {
            return;
        }

        // 克隆后在本控件的模板命名作用域里播放，才能解析到 PART_Thumb / PART_TrackOn
        var clone = storyboard.Clone();
        clone.Begin(this);
    }

    public CornerRadius TrackCornerRadius
    {
        get => (CornerRadius)GetValue(TrackCornerRadiusProperty);
        set => SetValue(TrackCornerRadiusProperty, value);
    }
}
