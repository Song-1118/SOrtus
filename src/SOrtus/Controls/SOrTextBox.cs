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

    /// <summary>回车提交命令（单行输入框按 Enter 时执行，例如「添加技术栈」）。多行输入不触发。</summary>
    public static readonly System.Windows.DependencyProperty SubmitCommandProperty =
        System.Windows.DependencyProperty.Register(
            nameof(SubmitCommand), typeof(System.Windows.Input.ICommand), typeof(SOrTextBox),
            new System.Windows.PropertyMetadata(null));

    /// <summary>
    /// 是否显示「清除」按钮（VD-085：搜索框有内容时给一个 ×，不必逐字删）。
    /// 多行输入不显示（有换行需求，误清代价高）。
    /// </summary>
    public static readonly System.Windows.DependencyProperty IsClearableProperty =
        System.Windows.DependencyProperty.Register(
            nameof(IsClearable), typeof(bool), typeof(SOrTextBox),
            new System.Windows.PropertyMetadata(false));

    /// <summary>
    /// 字段校验失败态（VD-091）：为 true 时边框换语义危险色，并保持到用户重新编辑。
    /// </summary>
    public static readonly System.Windows.DependencyProperty IsInvalidProperty =
        System.Windows.DependencyProperty.Register(
            nameof(IsInvalid), typeof(bool), typeof(SOrTextBox),
            new System.Windows.PropertyMetadata(false));

    /// <summary>清除按钮命令（模板里绑定，清空 <see cref="System.Windows.Controls.TextBox.Text"/>）。</summary>
    public static readonly System.Windows.DependencyProperty ClearCommandProperty =
        System.Windows.DependencyProperty.Register(
            nameof(ClearCommand), typeof(System.Windows.Input.ICommand), typeof(SOrTextBox),
            new System.Windows.PropertyMetadata(null));

    static SOrTextBox()
    {
        System.Windows.Controls.TextBox.DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SOrTextBox),
            new System.Windows.FrameworkPropertyMetadata(typeof(SOrTextBox)));
    }

    public SOrTextBox()
    {
        ClearCommand = new Common.RelayCommand(_ =>
        {
            SetCurrentValue(TextProperty, string.Empty);
            Focus();
        });
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

    public System.Windows.Input.ICommand? ClearCommand
    {
        get => (System.Windows.Input.ICommand?)GetValue(ClearCommandProperty);
        private set => SetValue(ClearCommandProperty, value);
    }

    /// <summary>
    /// 单行输入框回车触发 <see cref="SubmitCommand"/>。
    /// 说明：InputBinding 绑不到 DataContext（不在可视树内），所以回车提交在控件内部处理。
    /// </summary>
    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && !AcceptsReturn)
        {
            var command = SubmitCommand;
            if (command is not null && command.CanExecute(null))
            {
                command.Execute(null);
                e.Handled = true;
                return;
            }
        }

        base.OnKeyDown(e);
    }
}
