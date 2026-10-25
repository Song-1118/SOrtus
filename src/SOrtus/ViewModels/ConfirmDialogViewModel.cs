using System.Windows.Input;
using SOrtus.Common;
using SOrtus.Core.Services;

namespace SOrtus.ViewModels;

/// <summary>
/// 确认 / 提示对话框视图模型（配合主窗口覆盖层 + ViewTemplates 中
/// <c>vm:ConfirmDialogViewModel</c> 的 DataTemplate 渲染）。
/// </summary>
internal sealed class ConfirmDialogViewModel : ViewModelBase
{
    private readonly bool _isConfirm;

    /// <summary>对话框关闭事件；参数为结果（确认=true / 取消或提示=false）。</summary>
    public event Action<bool>? Closed;

    public string Title { get; }

    public string Message { get; }

    /// <summary>是否为「确认 / 取消」双按钮形态；false 为仅「确定」。</summary>
    public bool IsConfirm => _isConfirm;

    /// <summary>
    /// 破坏性操作（VD-117）：true 时主按钮用危险样式、标题左侧加危险图标，
    /// 且不设 IsDefault——默认焦点落在「取消」上，Enter 不会误触删除。
    /// </summary>
    public bool IsDestructive { get; }

    /// <summary>主按钮文本（确认=Common_Confirm，提示=Common_OK）。</summary>
    public string ConfirmText { get; }

    public string CancelText { get; }

    public System.Windows.Input.ICommand ConfirmCommand { get; }

    public System.Windows.Input.ICommand CancelCommand { get; }

    public ConfirmDialogViewModel(
        string title,
        string message,
        bool isConfirm,
        ILocalizationService localization,
        bool isDestructive = false)
    {
        Title = title;
        Message = message;
        _isConfirm = isConfirm;
        IsDestructive = isDestructive;
        ConfirmText = localization.GetString(isConfirm ? "Common_Confirm" : "Common_OK");
        CancelText = localization.GetString("Common_Cancel");

        ConfirmCommand = new RelayCommand(_ => Closed?.Invoke(true));
        CancelCommand = new RelayCommand(_ => Closed?.Invoke(false));
    }
}
