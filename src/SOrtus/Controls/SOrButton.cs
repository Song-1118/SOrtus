namespace SOrtus.Controls;

/// <summary>按钮视觉变体（设计令牌层：主 / 次 / 危险 / 幽灵 / 导航）。</summary>
public enum SOrButtonVariant
{
    /// <summary>主操作：Primary 底 + 反色文字。</summary>
    Primary,

    /// <summary>次要操作：Surface 底 + 描边。</summary>
    Secondary,

    /// <summary>危险操作：Danger 底 + 反色文字（删除 / 恢复）。</summary>
    Danger,

    /// <summary>幽灵按钮：透明底，用于低强调动作。</summary>
    Ghost,

    /// <summary>导航项：透明底 + 左对齐，IsActive 时高亮。</summary>
    Nav,

    /// <summary>可选卡片（向导类型卡）：低一阶底色 + 明确边框，IsActive 时 2px 主色环 + 右上角对勾。</summary>
    Card,

    /// <summary>版块 tab：次要色文字，IsActive 时主色文字 + 底部 2px 指示条。</summary>
    Tab,
}

/// <summary>
/// 自定义按钮（MVP 控件库 · 规范 7.1「样式先集中、控件再复用」）。
/// <para>
/// 视觉树内置四个命名层：<c>PART_Root</c>（背景）、<c>PART_Hover</c>（悬浮覆盖）、
/// <c>PART_Press</c>（按压覆盖）、<c>PART_FocusRing</c>（键盘焦点环）。
/// 后期做空间模糊（对 PART_Root 挂 BlurPanel）与动画（驱动 PART_Hover/PART_Press 的 Opacity）
/// 均无需改动控件代码。
/// </para>
/// <para>页面禁止内联样式细节，一律通过 Variant / Icon / IsActive 声明意图。</para>
/// </summary>
public sealed class SOrButton : System.Windows.Controls.Button
{
    /// <summary>视觉变体。</summary>
    public static readonly System.Windows.DependencyProperty VariantProperty =
        System.Windows.DependencyProperty.Register(
            nameof(Variant), typeof(SOrButtonVariant), typeof(SOrButton),
            new System.Windows.PropertyMetadata(SOrButtonVariant.Primary));

    /// <summary>前置图标（Geometry 路径数据，为空则不显示）。</summary>
    public static readonly System.Windows.DependencyProperty IconProperty =
        System.Windows.DependencyProperty.Register(
            nameof(Icon), typeof(System.Windows.Media.Geometry), typeof(SOrButton),
            new System.Windows.PropertyMetadata(null));

    /// <summary>图标尺寸。</summary>
    public static readonly System.Windows.DependencyProperty IconSizeProperty =
        System.Windows.DependencyProperty.Register(
            nameof(IconSize), typeof(double), typeof(SOrButton),
            new System.Windows.PropertyMetadata(14.0));

    /// <summary>圆角半径（缺省取令牌 CornerRadiusSmall）。</summary>
    public static readonly System.Windows.DependencyProperty CornerRadiusProperty =
        System.Windows.DependencyProperty.Register(
            nameof(CornerRadius), typeof(System.Windows.CornerRadius), typeof(SOrButton),
            new System.Windows.PropertyMetadata(new System.Windows.CornerRadius(6)));

    /// <summary>选中 / 当前态（导航高亮、类型卡片选中）。</summary>
    public static readonly System.Windows.DependencyProperty IsActiveProperty =
        System.Windows.DependencyProperty.Register(
            nameof(IsActive), typeof(bool), typeof(SOrButton),
            new System.Windows.PropertyMetadata(false));

    static SOrButton()
    {
        System.Windows.Controls.Primitives.ButtonBase.DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SOrButton),
            new System.Windows.FrameworkPropertyMetadata(typeof(SOrButton)));
    }

    public SOrButtonVariant Variant
    {
        get => (SOrButtonVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public System.Windows.Media.Geometry? Icon
    {
        get => (System.Windows.Media.Geometry?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
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

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }
}
