namespace SOrtus.Controls;

/// <summary>
/// 自定义下拉选择框（MVP 控件库）。
/// <para>
/// 字段区 + 下拉浮层均为自定义视觉树：<c>PART_Field</c>（字段容器）、<c>PART_Placeholder</c>（无选中提示）、
/// <c>PART_DropDownToggle</c>（整字段可点的开关，绑定 IsDropDownOpen）、<c>PART_Popup</c>（下拉浮层，原生约定）。
/// 浮层内容为 Surface 卡片 + 自定义 <see cref="System.Windows.Controls.ComboBoxItem"/> 容器样式。
/// </para>
/// </summary>
public sealed class SOrComboBox : System.Windows.Controls.ComboBox
{
    /// <summary>无选中项时的提示文本。</summary>
    public static readonly System.Windows.DependencyProperty PlaceholderProperty =
        System.Windows.DependencyProperty.Register(
            nameof(Placeholder), typeof(string), typeof(SOrComboBox),
            new System.Windows.PropertyMetadata(string.Empty));

    /// <summary>圆角半径（缺省取令牌 CornerRadiusSmall）。</summary>
    public static readonly System.Windows.DependencyProperty CornerRadiusProperty =
        System.Windows.DependencyProperty.Register(
            nameof(CornerRadius), typeof(System.Windows.CornerRadius), typeof(SOrComboBox),
            new System.Windows.PropertyMetadata(new System.Windows.CornerRadius(6)));

    /// <summary>字段校验失败态（VD-091）：边框换语义危险色。</summary>
    public static readonly System.Windows.DependencyProperty IsInvalidProperty =
        System.Windows.DependencyProperty.Register(
            nameof(IsInvalid), typeof(bool), typeof(SOrComboBox),
            new System.Windows.PropertyMetadata(false));

    static SOrComboBox()
    {
        System.Windows.Controls.ComboBox.DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SOrComboBox),
            new System.Windows.FrameworkPropertyMetadata(typeof(SOrComboBox)));

        // 占位符可见性由代码控制，不用模板 DataTrigger：实测选中枚举项后
        // DataTrigger（SelectedItem==null）不退出，占位符「全部类型」与选中内容
        // 同时显示在框里——用户报的「选择后选择的内容和原始内容重叠」正是这个。
        SelectedItemProperty.OverrideMetadata(
            typeof(SOrComboBox),
            new System.Windows.FrameworkPropertyMetadata(OnSelectedItemChanged));
    }

    private static void OnSelectedItemChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (d is SOrComboBox combo)
        {
            combo.UpdatePlaceholder();
        }
    }

    /// <summary>有选中项时隐藏占位符，无选中项时显示。</summary>
    private void UpdatePlaceholder()
    {
        if (Template?.FindName("PART_Placeholder", this) is not System.Windows.FrameworkElement placeholder)
        {
            return;
        }

        placeholder.Visibility = SelectedItem is null
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdatePlaceholder();
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
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
}
