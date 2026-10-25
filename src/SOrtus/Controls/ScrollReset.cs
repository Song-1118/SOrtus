using System.Windows;
using System.Windows.Controls;

namespace SOrtus.Controls;

/// <summary>
/// 版块切换回顶（VD-099）。
/// <para>
/// 详情页三个版块共用同一个 ScrollViewer，切换只改内部内容的 Visibility，
/// 滚动位置会被沿用——从「基础信息」滚到底再切「开发管理」，新内容直接从中段开始，
/// 标题、tab、第一个字段全在视口外，看起来像页面坏了。
/// 这里把「当前版块」作为触发源绑定进来：值一变就把滚动位置归零。
/// </para>
/// </summary>
public static class ScrollReset
{
    /// <summary>触发源（一般绑当前版块标识）；值变化即回顶。</summary>
    public static readonly DependencyProperty TriggerProperty =
        DependencyProperty.RegisterAttached(
            "Trigger", typeof(object), typeof(ScrollReset),
            new PropertyMetadata(null, OnTriggerChanged));

    public static object? GetTrigger(DependencyObject obj) => obj.GetValue(TriggerProperty);

    public static void SetTrigger(DependencyObject obj, object? value) => obj.SetValue(TriggerProperty, value);

    private static void OnTriggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer viewer || e.OldValue is null)
        {
            // 首次赋值（模板初次套用）不回顶，避免首屏跳动
            return;
        }

        viewer.ScrollToVerticalOffset(0);
    }
}
