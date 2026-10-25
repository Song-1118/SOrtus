using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SOrtus.Controls;

/// <summary>
/// 滚动位置记忆（VD-079）。
/// <para>
/// 列表页视图每次导航都会被 DataTemplate 重新实例化，ScrollViewer 的滚动位置随之丢失——
/// 用户从详情返回列表时就回到顶部，得重新找刚才那一条。
/// 这里把纵向偏移量存到视图模型（<c>Mode=TwoWay</c> 绑定），视图重建后由附加属性还原。
/// </para>
/// <para>
/// 还原时机放在 Loaded 之后的一个调度帧：此时 ItemsControl 已完成首次布局，
/// ScrollViewer 的 ExtentHeight 才是最终值，直接 ScrollToVerticalOffset 会被夹到 0。
/// </para>
/// </summary>
public static class ScrollMemory
{
    /// <summary>纵向滚动偏移（与视图模型双向绑定）。</summary>
    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.RegisterAttached(
            "VerticalOffset", typeof(double), typeof(ScrollMemory),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnOffsetChanged));

    /// <summary>内部标记：事件只挂一次。</summary>
    private static readonly DependencyProperty IsHookedProperty =
        DependencyProperty.RegisterAttached("IsHooked", typeof(bool), typeof(ScrollMemory), new PropertyMetadata(false));

    public static double GetVerticalOffset(DependencyObject obj) => (double)obj.GetValue(VerticalOffsetProperty);

    public static void SetVerticalOffset(DependencyObject obj, double value) => obj.SetValue(VerticalOffsetProperty, value);

    private static void OnOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer viewer || (bool)viewer.GetValue(IsHookedProperty))
        {
            return;
        }

        viewer.SetValue(IsHookedProperty, true);
        viewer.Loaded += OnLoaded;
        viewer.ScrollChanged += OnScrollChanged;

        if (viewer.IsLoaded)
        {
            Restore(viewer);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer viewer)
        {
            Restore(viewer);
        }
    }

    private static void Restore(ScrollViewer viewer)
    {
        var target = GetVerticalOffset(viewer);
        if (target <= 0)
        {
            return;
        }

        viewer.Dispatcher.BeginInvoke(
            new Action(() =>
            {
                if (viewer.ScrollableHeight >= target)
                {
                    viewer.ScrollToVerticalOffset(target);
                }
            }),
            DispatcherPriority.Loaded);
    }

    private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer viewer || Math.Abs(e.VerticalOffset - GetVerticalOffset(viewer)) < 0.5)
        {
            return;
        }

        // 写回视图模型：绑定是 TwoWay，SetValue 会同步到 VM.ScrollOffset
        SetVerticalOffset(viewer, e.VerticalOffset);
    }
}
