using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SOrtus.Core.Models;

namespace SOrtus.Controls;

/// <summary>
/// 自定义立项列表项（MVP 控件库 · 列表行统一组件）。
/// <para>
/// 单行展示：名称、更新时间、类型 / 状态双胶囊（<see cref="SOrTag"/>）、进度条（<see cref="SOrProgressBar"/>）、
/// 打开按钮（<see cref="SOrButton"/>）。交互：单击选中（宿主 ListView 负责）、双击打开、
/// 点击「打开」按钮打开，三条路径均走 <see cref="OpenCommand"/>。
/// </para>
/// <para>
/// 命名层：<c>PART_Root</c>（行容器）、<c>PART_Hover</c>（悬浮覆盖层，动画改 Opacity）。
/// 行容器不带独立模糊（规范 7.7：列表项轻量，模糊只在外层卡片）。
/// </para>
/// </summary>
public sealed class SOrProjectListItem : ContentControl
{
    /// <summary>绑定的立项实体。</summary>
    public static readonly DependencyProperty ProjectProperty =
        DependencyProperty.Register(
            nameof(Project), typeof(Project), typeof(SOrProjectListItem),
            new PropertyMetadata(null));

    /// <summary>打开详情命令（CommandParameter 自动传 <see cref="Project"/>）。</summary>
    public static readonly DependencyProperty OpenCommandProperty =
        DependencyProperty.Register(
            nameof(OpenCommand), typeof(ICommand), typeof(SOrProjectListItem),
            new PropertyMetadata(null));

    static SOrProjectListItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SOrProjectListItem), new FrameworkPropertyMetadata(typeof(SOrProjectListItem)));
    }

    public Project? Project
    {
        get => (Project?)GetValue(ProjectProperty);
        set => SetValue(ProjectProperty, value);
    }

    public ICommand? OpenCommand
    {
        get => (ICommand?)GetValue(OpenCommandProperty);
        set => SetValue(OpenCommandProperty, value);
    }

    /// <summary>
    /// 单击即打开（VD-084）。
    /// 手型光标是一份「这里可以点」的承诺，而旧版只绑了双击——单击毫无反应，用户会以为点坏了。
    /// <para>
    /// 用 Preview 阶段拦截：若命中的是行内按钮（「打开」），直接放行给按钮自己处理，
    /// 否则整个行按钮化会被内层按钮的点击「吃掉」或反之触发两次。
    /// </para>
    /// </summary>
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);

        if (e.Handled || Project is null || IsFromButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var command = OpenCommand;
        if (command?.CanExecute(Project) == true)
        {
            command.Execute(Project);
            e.Handled = true;
        }
    }

    /// <summary>命中链上是否有按钮 / 开关（含「打开」按钮与标签内的 × ）。</summary>
    private static bool IsFromButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase)
            {
                return true;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
