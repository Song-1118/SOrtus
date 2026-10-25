using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace SOrtus.Controls;

/// <summary>
/// 鼠标滚轮灵敏度（用户反馈：预览 MD 文档时滚轮太慢）。
/// <para>
/// WPF 默认每格滚轮滚 3 行（<c>SystemParameters.WheelScrollLines</c>），在文档阅读区偏慢；
/// 这里接管滚轮事件，按「每格 N 行」换算成像素直接改滚动偏移，行高按正文排版取 24px
/// （与规范 VD-071 的正文行高一致），并把 <c>e.Handled</c> 置真，避免系统再滚一次造成双倍位移。
/// </para>
/// </summary>
public static class MouseWheelScroll
{
    /// <summary>每格滚轮滚多少行（默认 3 = 系统默认）。</summary>
    public static readonly DependencyProperty LinesPerNotchProperty =
        DependencyProperty.RegisterAttached(
            "LinesPerNotch", typeof(int), typeof(MouseWheelScroll),
            new PropertyMetadata(3, OnLinesPerNotchChanged));

    public static int GetLinesPerNotch(DependencyObject obj) => (int)obj.GetValue(LinesPerNotchProperty);
    public static void SetLinesPerNotch(DependencyObject obj, int value) => obj.SetValue(LinesPerNotchProperty, value);

    /// <summary>文档正文行高（与 VD-071 的正文行高一致）。</summary>
    private const double DocumentLineHeight = 24;

    /// <summary>普通内容区行高（列表等）。</summary>
    private const double DefaultLineHeight = 16;

    private static void OnLinesPerNotchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        element.PreviewMouseWheel -= OnPreviewMouseWheel;
        element.PreviewMouseWheel += OnPreviewMouseWheel;
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta == 0 || sender is not UIElement element)
        {
            return;
        }

        var viewer = element as ScrollViewer ?? FindAncestor<ScrollViewer>(element);
        if (viewer is null)
        {
            return;
        }

        double lineHeight = viewer.GetType() == typeof(FlowDocumentScrollViewer) ? DocumentLineHeight : DefaultLineHeight;
        double step = Math.Max(1, GetLinesPerNotch(element)) * lineHeight;

        // 一格滚轮 = 120 单位的 Delta
        double delta = -e.Delta / 120.0 * step;
        double target = viewer.VerticalOffset + delta;

        // 夹在可视范围内，避免滚过头
        double max = Math.Max(0, viewer.ExtentHeight - viewer.ViewportHeight);
        target = Math.Clamp(target, 0, max);

        viewer.ScrollToVerticalOffset(target);
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
    {
        for (var parent = System.Windows.Media.VisualTreeHelper.GetParent(start);
             parent is not null;
             parent = System.Windows.Media.VisualTreeHelper.GetParent(parent))
        {
            if (parent is T found)
            {
                return found;
            }
        }

        return null;
    }
}
