using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SOrtus.Controls;

/// <summary>
/// 自定义日期选择器（MVP 控件库）。
/// <para>
/// 自定义字段外壳（<c>PART_FieldBorder</c> + <c>PART_TextBox</c> + <c>PART_Button</c>，均为原生约定部件），
/// 下拉日历通过 <see cref="System.Windows.Controls.DatePicker.CalendarStyle"/> 挂 Surface 风格样式。
/// 注意：<c>PART_Popup</c> 内不写内容——DatePicker 在 OnApplyTemplate 时会把内置 Calendar 赋给 Popup.Child。
/// </para>
/// <para>
/// VD-068：Popup.Child 由 DatePicker 在模板内部创建，XAML 里够不到，因此日历浮层的
/// 「8px 上移 + 150ms 淡入」在这里对 Popup.Child 施加，与下拉框浮层（纯 XAML 实现）保持同一节奏。
/// </para>
/// </summary>
public sealed class SOrDatePicker : System.Windows.Controls.DatePicker
{
    /// <summary>圆角半径（缺省取令牌 CornerRadiusSmall）。</summary>
    public static readonly System.Windows.DependencyProperty CornerRadiusProperty =
        System.Windows.DependencyProperty.Register(
            nameof(CornerRadius), typeof(System.Windows.CornerRadius), typeof(SOrDatePicker),
            new System.Windows.PropertyMetadata(new System.Windows.CornerRadius(6)));

    /// <summary>字段校验失败态（VD-091）：边框换语义危险色。</summary>
    public static readonly System.Windows.DependencyProperty IsInvalidProperty =
        System.Windows.DependencyProperty.Register(
            nameof(IsInvalid), typeof(bool), typeof(SOrDatePicker),
            new System.Windows.PropertyMetadata(false));

    static SOrDatePicker()
    {
        System.Windows.Controls.DatePicker.DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SOrDatePicker),
            new System.Windows.FrameworkPropertyMetadata(typeof(SOrDatePicker)));
    }

    public System.Windows.CornerRadius CornerRadius
    {
        get => (System.Windows.CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public bool IsInvalid
    {
        get => (bool)GetValue(IsInvalidProperty);
        set => SetValue(IsInvalidProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // VD-068：日历由 DatePicker 内部创建并直接塞进 PART_Popup.Child，
        // 模板里够不到，因此这里挂在 Popup.Opened 上，展开瞬间给 Child（Calendar）加入场动画。
        if (GetTemplateChild("PART_Popup") is Popup popup)
        {
            popup.Opened -= OnPopupOpened;
            popup.Opened += OnPopupOpened;
        }
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        if (sender is not Popup { Child: FrameworkElement child })
        {
            return;
        }

        child.RenderTransform = new TranslateTransform(0, 8);
        child.Opacity = 0;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        child.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = ease,
        });

        if (child.RenderTransform is TranslateTransform transform)
        {
            transform.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease });
        }
    }
}
