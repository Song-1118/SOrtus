namespace SOrtus.Controls;

/// <summary>
/// 自定义单行 / 多行文本框（MVP 控件库）。
/// <para>
/// 在原生 TextBox 行为之上补齐占位符（<see cref="Placeholder"/>）与前置图标（<see cref="LeadingIcon"/>）。
/// 视觉树命名层：<c>PART_Root</c>（边框容器）、<c>PART_Icon</c>（图标）、
/// <c>PART_ContentHost</c>（内容宿主，原生约定）、<c>PART_Placeholder</c>（占位符）。
/// </para>
/// </summary>
public sealed class SOrTextBox : System.Windows.Controls.TextBox
{
    /// <summary>占位符文本（内容为空且未聚焦时显示）。</summary>
    public static readonly System.Windows.DependencyProperty PlaceholderProperty =
        System.Windows.DependencyProperty.Register(
            nameof(Placeholder), typeof(string), typeof(SOrTextBox),
            new System.Windows.PropertyMetadata(string.Empty));

    /// <summary>前置图标（Geometry 路径数据，为空则不显示）。</summary>
    public static readonly System.Windows.DependencyProperty LeadingIconProperty =
        System.Windows.DependencyProperty.Register(
            nameof(LeadingIcon), typeof(System.Windows.Media.Geometry), typeof(SOrTextBox),
            new System.Windows.PropertyMetadata(null));

    /// <summary>图标尺寸。</summary>
    public static readonly System.Windows.DependencyProperty IconSizeProperty =
        System.Windows.DependencyProperty.Register(
            nameof(IconSize), typeof(double), typeof(SOrTextBox),
            new System.Windows.PropertyMetadata(14.0));

    /// <summary>圆角半径（缺省取令牌 CornerRadiusSmall）。</summary>
    public static readonly System.Windows.DependencyProperty CornerRadiusProperty =
        System.Windows.DependencyProperty.Register(
            nameof(CornerRadius), typeof(System.Windows.CornerRadius), typeof(SOrTextBox),
            new System.Windows.PropertyMetadata(new System.Windows.CornerRadius(6)));

    static SOrTextBox()
    {
        System.Windows.Controls.TextBox.DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SOrTextBox),
            new System.Windows.FrameworkPropertyMetadata(typeof(SOrTextBox)));
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public System.Windows.Media.Geometry? LeadingIcon
    {
        get => (System.Windows.Media.Geometry?)GetValue(LeadingIconProperty);
        set => SetValue(LeadingIconProperty, value);
    }

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public System.Windows.CornerRadius CornerRadius
    {
        get => (System.Windows.CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }
}
