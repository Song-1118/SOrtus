namespace SOrtus.Controls;

/// <summary>
/// 自定义日期选择器（MVP 控件库）。
/// <para>
/// 自定义字段外壳（<c>PART_FieldBorder</c> + <c>PART_TextBox</c> + <c>PART_Button</c>，均为原生约定部件），
/// 下拉日历通过 <see cref="System.Windows.Controls.DatePicker.CalendarStyle"/> 挂 Surface 风格样式。
/// 注意：<c>PART_Popup</c> 内不写内容——DatePicker 在 OnApplyTemplate 时会把内置 Calendar 赋给 Popup.Child。
/// </para>
/// </summary>
public sealed class SOrDatePicker : System.Windows.Controls.DatePicker
{
    /// <summary>圆角半径（缺省取令牌 CornerRadiusSmall）。</summary>
    public static readonly System.Windows.DependencyProperty CornerRadiusProperty =
        System.Windows.DependencyProperty.Register(
            nameof(CornerRadius), typeof(System.Windows.CornerRadius), typeof(SOrDatePicker),
            new System.Windows.PropertyMetadata(new System.Windows.CornerRadius(6)));

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
}
