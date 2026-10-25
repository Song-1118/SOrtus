using System.Windows.Input;

namespace SOrtus.ViewModels;

/// <summary>
/// 覆盖层对话框视图模型契约（规范 7.7 红线 #5：对话框统一在主窗口内承载）。
/// <para>
/// 实现者通过 <see cref="Closed"/> 通知宿主收起遮罩；主窗口的 Esc / 点击遮罩走
/// <see cref="RequestClose"/>，因此「取消」与「关闭」语义一致。
/// </para>
/// </summary>
public interface IOverlayDialogViewModel
{
    /// <summary>对话框请求关闭（宿主据此移除遮罩并完成等待中的任务）。</summary>
    event Action? Closed;

    /// <summary>
    /// 点击遮罩是否等同关闭（VD-083）。
    /// 确认 / 提示类弹窗没有可丢失的内容，点遮罩关闭是安全的（默认 true）；
    /// 带输入的配置类弹窗必须显式返回 false，避免误点一下就把已填内容丢掉。
    /// </summary>
    bool AllowScrimClose => true;

    /// <summary>请求关闭（Esc / 点击遮罩 / 关闭按钮）。</summary>
    void RequestClose();
}

/// <summary>带关闭命令的覆盖层对话框（便于 XAML 绑定关闭按钮）。</summary>
public interface IClosableOverlayDialogViewModel : IOverlayDialogViewModel
{
    ICommand CloseCommand { get; }
}
