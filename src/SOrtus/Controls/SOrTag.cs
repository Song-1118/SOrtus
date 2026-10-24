using System.Windows;
using System.Windows.Controls;

namespace SOrtus.Controls;

/// <summary>标签色调（设计令牌：三类立项色 + 语义状态色）。</summary>
public enum SOrTagTone
{
    /// <summary>强调色 A（靛紫 · 编程开发）。</summary>
    AccentA,

    /// <summary>强调色 B（天蓝 · 待办 / 信息）。</summary>
    AccentB,

    /// <summary>强调色 C（粉 · 剪辑）。</summary>
    AccentC,

    /// <summary>成功。</summary>
    Success,

    /// <summary>警告。</summary>
    Warning,

    /// <summary>危险。</summary>
    Danger,

    /// <summary>中性。</summary>
    Neutral,
}

/// <summary>
/// 自定义胶囊标签（MVP 控件库）：类型 / 状态 / 统计胶囊统一组件。
/// <para>命名层：<c>PART_Root</c>（胶囊容器）。色调通过 <see cref="Tone"/> 声明，底色 / 文字色成对取自主题令牌。</para>
/// </summary>
public sealed class SOrTag : ContentControl
{
    /// <summary>色调。</summary>
    public static readonly DependencyProperty ToneProperty =
        DependencyProperty.Register(
            nameof(Tone), typeof(SOrTagTone), typeof(SOrTag),
            new PropertyMetadata(SOrTagTone.AccentA));

    /// <summary>圆角半径（缺省 Pill）。</summary>
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius), typeof(CornerRadius), typeof(SOrTag),
            new PropertyMetadata(new CornerRadius(999)));

    static SOrTag()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SOrTag), new FrameworkPropertyMetadata(typeof(SOrTag)));
    }

    public SOrTagTone Tone
    {
        get => (SOrTagTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }
}
