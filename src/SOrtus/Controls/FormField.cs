using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SOrtus.Controls;

/// <summary>
/// 表单字段锚点与「滚动到位 + 聚焦 + 抖动」（VD-091）。
/// <para>
/// 向导视图挂在 DataTemplate 里，没有代码后置，视图模型无法直接引用控件；
/// 这里用附加属性给字段打一个字符串锚点（<c>Name</c> / <c>TechStack</c> / …），
/// 宿主窗口在收到视图模型的锚点请求后按锚点在视觉树里定位控件。
/// </para>
/// </summary>
public static class FormField
{
    /// <summary>字段锚点（同一容器内唯一）。</summary>
    public static readonly DependencyProperty AnchorProperty =
        DependencyProperty.RegisterAttached(
            "Anchor", typeof(string), typeof(FormField), new PropertyMetadata(null));

    public static string? GetAnchor(DependencyObject obj) => (string?)obj.GetValue(AnchorProperty);

    public static void SetAnchor(DependencyObject obj, string? value) => obj.SetValue(AnchorProperty, value);

    /// <summary>在指定根元素的子树里按锚点查找第一个匹配的元素。</summary>
    public static FrameworkElement? Find(DependencyObject? root, string anchor)
    {
        if (root is null || string.IsNullOrEmpty(anchor))
        {
            return null;
        }

        if (root is FrameworkElement element && string.Equals(GetAnchor(element), anchor, StringComparison.Ordinal))
        {
            return element;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = Find(VisualTreeHelper.GetChild(root, i), anchor);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// 定位错误字段：滚动进视野 → 聚焦 → 160ms 抖动（位移 ≤2px）。
    /// 抖动只是「看这里」的提示，不做大幅 shake，避免与磨砂卡片的克制基调冲突。
    /// </summary>
    public static void RevealAndFocus(FrameworkElement target)
    {
        target.BringIntoView();

        var box = FindFocusable(target);
        box?.Focus();
        Keyboard.Focus(box ?? target);

        Shake(target);
    }

    /// <summary>输入控件本身可聚焦；纯容器则找内部第一个可聚焦子元素。</summary>
    private static IInputElement? FindFocusable(DependencyObject root)
    {
        if (root is IInputElement { Focusable: true } input)
        {
            return input;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FindFocusable(VisualTreeHelper.GetChild(root, i));
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static void Shake(FrameworkElement target)
    {
        if (target.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform();
            target.RenderTransform = transform;
        }

        var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(160) };
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(-2, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(40))));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(2, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90))));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160))));

        transform.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    /// <summary>在对话框里找第一个可编辑字段；没有则退回第一个按钮（VD-081）。</summary>
    public static IInputElement? FindFirstField(DependencyObject? root)
    {
        return FindFirst(root, preferField: true) ?? FindFirst(root, preferField: false);
    }

    private static IInputElement? FindFirst(DependencyObject? root, bool preferField)
    {
        if (root is null)
        {
            return null;
        }

        var isField = root is TextBoxBase or ComboBox or DatePicker;
        var isButton = root is ButtonBase;
        if ((preferField && isField) || (!preferField && (isButton || isField)))
        {
            if (root is IInputElement { Focusable: true } input)
            {
                return input;
            }
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FindFirst(VisualTreeHelper.GetChild(root, i), preferField);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
