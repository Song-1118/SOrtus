using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SOrtus.Controls;

/// <summary>
/// 自定义单行 / 多行文本框（控件库）。
/// <para>
/// <b>为什么外壳是 ContentControl、内部放原生 TextBox</b>：
/// 直接继承 TextBox 再换模板时，编辑区（内部 <c>TextBoxView</c>）虽然被创建出来、也有尺寸，
/// 但它的**输入焦点接不上**——实测现象是：焦点能切到框上，但键盘输入被吞、点击时编辑区整块发黑；
/// 而框里**已有文本**时又能正常编辑。根因在 TextBox 自己的编辑区与自定义模板的配合，
/// 属于不可控的内部实现细节。
/// </para>
/// <para>
/// 现在改成：外壳 <see cref="ContentControl"/> 只负责边框、占位符、前置图标、清除按钮与校验态外观，
/// 内部放一个**原生 TextBox**（命名部件 <c>PART_ContentHost</c>，使用系统模板）：
/// 光标定位、输入、选中、IME、撤销全部走 WPF 原生实现，行为与系统输入框一致。
/// </para>
/// <para>
/// 对外 API 与绑定写法不变：<c>Text</c> / <c>Placeholder</c> / <c>LeadingIcon</c> / <c>IsClearable</c> /
/// <c>IsInvalid</c> / <c>SubmitCommand</c> / <c>AcceptsReturn</c>，页面无需改动。
/// </para>
/// </summary>
public sealed class SOrTextBox : ContentControl
{
    /// <summary>
    /// 文本内容（与内部原生 TextBox 双向同步）。
    /// <para>
    /// 元数据必须带 <see cref="FrameworkPropertyMetadataOptions.BindsTwoWayByDefault"/>：
    /// 系统 TextBox.Text 自带该标志，所以页面里 <c>Text="{Binding SearchText}"</c>
    /// 省略 Mode 也是双向；自定义 DP 不写就是 OneWay——那用户打字只传到外壳，
    /// 视图模型永远收不到（「搜索功能无效」的根因：列表页的搜索框正是这种写法）。
    /// </para>
    /// </summary>
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(SOrTextBox),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

    /// <summary>占位符文本（内容为空且未聚焦时显示）。</summary>
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(
            nameof(Placeholder), typeof(string), typeof(SOrTextBox),
            new PropertyMetadata(string.Empty));

    /// <summary>前置图标（Geometry 路径数据，为空则不显示）。</summary>
    public static readonly DependencyProperty LeadingIconProperty =
        DependencyProperty.Register(
            nameof(LeadingIcon), typeof(System.Windows.Media.Geometry), typeof(SOrTextBox),
            new PropertyMetadata(null));

    /// <summary>图标尺寸。</summary>
    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(
            nameof(IconSize), typeof(double), typeof(SOrTextBox),
            new PropertyMetadata(14.0));

    /// <summary>圆角半径（缺省取令牌 CornerRadiusSmall）。</summary>
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius), typeof(System.Windows.CornerRadius), typeof(SOrTextBox),
            new PropertyMetadata(new System.Windows.CornerRadius(6)));

    /// <summary>回车提交命令（单行输入框按 Enter 时执行，例如「添加技术栈」）。多行不触发。</summary>
    public static readonly DependencyProperty SubmitCommandProperty =
        DependencyProperty.Register(
            nameof(SubmitCommand), typeof(System.Windows.Input.ICommand), typeof(SOrTextBox),
            new PropertyMetadata(null));

    /// <summary>是否允许回车换行（多行输入场景，如「简介」「开发规划」）。</summary>
    public static readonly DependencyProperty AcceptsReturnProperty =
        DependencyProperty.Register(
            nameof(AcceptsReturn), typeof(bool), typeof(SOrTextBox),
            new PropertyMetadata(false));

    /// <summary>
    /// 是否显示「清除」按钮（VD-085：搜索框有内容时给一个 ×，不必逐字删）。
    /// 多行输入不显示（有换行需求，误清代价高）。
    /// </summary>
    public static readonly DependencyProperty IsClearableProperty =
        DependencyProperty.Register(
            nameof(IsClearable), typeof(bool), typeof(SOrTextBox),
            new PropertyMetadata(false));

    /// <summary>字段校验失败态（VD-091）：为 true 时边框换语义危险色，并保持到用户重新编辑。</summary>
    public static readonly DependencyProperty IsInvalidProperty =
        DependencyProperty.Register(
            nameof(IsInvalid), typeof(bool), typeof(SOrTextBox),
            new PropertyMetadata(false));

    /// <summary>选中高亮色（转发给内部原生输入框）。</summary>
    public static readonly DependencyProperty SelectionBrushProperty =
        DependencyProperty.Register(
            nameof(SelectionBrush), typeof(System.Windows.Media.Brush), typeof(SOrTextBox),
            new PropertyMetadata(null));

    /// <summary>光标颜色（转发给内部原生输入框）。</summary>
    public static readonly DependencyProperty CaretBrushProperty =
        DependencyProperty.Register(
            nameof(CaretBrush), typeof(System.Windows.Media.Brush), typeof(SOrTextBox),
            new PropertyMetadata(null));

    /// <summary>文本换行方式（转发给内部原生输入框）。单行默认 NoWrap。</summary>
    public static readonly DependencyProperty TextWrappingProperty =
        DependencyProperty.Register(
            nameof(TextWrapping), typeof(TextWrapping), typeof(SOrTextBox),
            new PropertyMetadata(TextWrapping.NoWrap));

    /// <summary>清除按钮命令（模板里绑定，清空文本）。</summary>
    public static readonly DependencyProperty ClearCommandProperty =
        DependencyProperty.Register(
            nameof(ClearCommand), typeof(System.Windows.Input.ICommand), typeof(SOrTextBox),
            new PropertyMetadata(null));

    static SOrTextBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SOrTextBox),
            new FrameworkPropertyMetadata(typeof(SOrTextBox)));
    }

    public SOrTextBox()
    {
        ClearCommand = new Common.RelayCommand(_ =>
        {
            Text = string.Empty;
            Focus();
        });
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
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

    public System.Windows.Input.ICommand? SubmitCommand
    {
        get => (System.Windows.Input.ICommand?)GetValue(SubmitCommandProperty);
        set => SetValue(SubmitCommandProperty, value);
    }

    public bool AcceptsReturn
    {
        get => (bool)GetValue(AcceptsReturnProperty);
        set => SetValue(AcceptsReturnProperty, value);
    }

    public bool IsClearable
    {
        get => (bool)GetValue(IsClearableProperty);
        set => SetValue(IsClearableProperty, value);
    }

    public bool IsInvalid
    {
        get => (bool)GetValue(IsInvalidProperty);
        set => SetValue(IsInvalidProperty, value);
    }

    public System.Windows.Media.Brush? SelectionBrush
    {
        get => (System.Windows.Media.Brush?)GetValue(SelectionBrushProperty);
        set => SetValue(SelectionBrushProperty, value);
    }

    public System.Windows.Media.Brush? CaretBrush
    {
        get => (System.Windows.Media.Brush?)GetValue(CaretBrushProperty);
        set => SetValue(CaretBrushProperty, value);
    }

    public TextWrapping TextWrapping
    {
        get => (TextWrapping)GetValue(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    public System.Windows.Input.ICommand? ClearCommand
    {
        get => (System.Windows.Input.ICommand?)GetValue(ClearCommandProperty);
        private set => SetValue(ClearCommandProperty, value);
    }

    /// <summary>内部原生输入框（命名部件 PART_ContentHost）。</summary>
    public TextBox? Inner => Template?.FindName("PART_ContentHost", this) as TextBox;

    /// <summary>把焦点交给内部原生输入框（外壳本身不可获焦）。</summary>
    public new bool Focus() => Inner?.Focus() ?? false;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        var inner = Inner;
        if (inner is null)
        {
            return;
        }

        // 外部改 Text（页面绑定写回 / 清除命令）时同步到内部输入框
        if (inner.Text != Text)
        {
            inner.Text = Text;
        }

        // 单行回车提交；多行（AcceptsReturn）不拦截
        inner.KeyDown -= OnInnerKeyDown;
        inner.KeyDown += OnInnerKeyDown;

        // 清除按钮可见性：内部输入框有内容才显示（模板触发器无法用 ElementName 表达这件事）
        inner.TextChanged -= OnInnerTextChanged;
        inner.TextChanged += OnInnerTextChanged;
        UpdateClearButton();
    }

    private void OnInnerTextChanged(object sender, TextChangedEventArgs e) => UpdateClearButton();

    /// <summary>按「可清除 + 非多行 + 有内容」更新清除按钮的可见性。</summary>
    private void UpdateClearButton()
    {
        if (Template?.FindName("PART_Clear", this) is not FrameworkElement clear)
        {
            return;
        }

        bool visible = IsClearable && !AcceptsReturn && !string.IsNullOrEmpty(Text);
        clear.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnInnerKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || AcceptsReturn)
        {
            return;
        }

        var command = SubmitCommand;
        if (command is not null && command.CanExecute(null))
        {
            command.Execute(null);
            e.Handled = true;
        }
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SOrTextBox box)
        {
            if (box.Inner is { } inner && inner.Text != (string?)e.NewValue)
            {
                inner.Text = (string?)e.NewValue ?? string.Empty;
            }

            box.UpdateClearButton();
        }
    }
}
