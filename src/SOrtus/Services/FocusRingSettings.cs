using System.Windows;
using System.Windows.Media;

namespace SOrtus.Services;

/// <summary>
/// 键盘焦点框总开关（设置页「显示焦点框」，默认开）。
/// <para>
/// 为什么做成资源级开关而不是逐控件判断：全站所有控件模板里的焦点层都叫 <c>PART_FocusRing</c>，
/// 且统一用 <c>FocusRingBrush</c> 取色（见 Brushes.xaml）。把这一支画刷在应用级覆盖成透明，
/// 就能一次关掉按钮、输入框、下拉框、日期框、滑块、列表项、导航项的焦点框，
/// 不需要在十几处模板里各写一个 IsEnabled 触发器，也不会漏。
/// </para>
/// <para>
/// 关闭后仍保留两件事：输入框自己的 1px 描边（BorderStrongBrush，≥3:1）与文本光标。
/// 也就是说「键盘焦点在哪」仍然可辨，只是不再叠一层额外的强调环。
/// </para>
/// </summary>
internal static class FocusRingSettings
{
    private const string BrushKey = "FocusRingBrush";

    /// <summary>当前是否显示焦点框（仅反映本进程状态，持久化在 AppSettings）。</summary>
    public static bool IsEnabled { get; private set; } = true;

    /// <summary>
    /// 应用开关。<paramref name="enabled"/> 为 true 时移除应用级覆盖，让主题字典里的中间灰度生效；
    /// 为 false 时在应用级写入透明画刷——根字典的键优先级高于合并字典，换主题也不会被覆盖回来。
    /// </summary>
    public static void Apply(bool enabled)
    {
        IsEnabled = enabled;

        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        if (enabled)
        {
            app.Resources.Remove(BrushKey);
        }
        else
        {
            var transparent = new SolidColorBrush(Colors.Transparent);
            transparent.Freeze();
            app.Resources[BrushKey] = transparent;
        }
    }
}
