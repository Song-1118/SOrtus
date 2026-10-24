using System.Windows.Input;
using SOrtus.Common;
using SOrtus.Core.Services;

namespace SOrtus.ViewModels;

/// <summary>未保存更改的处理选择（UI 方案 5.2：保存 / 不保存 / 取消）。</summary>
public enum UnsavedChangesChoice
{
    /// <summary>保存后离开。</summary>
    Save,

    /// <summary>放弃更改直接离开。</summary>
    Discard,

    /// <summary>留在当前页面继续编辑。</summary>
    Cancel,
}

/// <summary>
/// 未保存更改三选对话框视图模型（验收 A12）。
/// 与「确认 / 取消」双按钮的 <see cref="ConfirmDialogViewModel"/> 分开，避免把多态塞进一个 VM。
/// </summary>
internal sealed class UnsavedChangesDialogViewModel : ViewModelBase
{
    /// <summary>对话框关闭事件；参数为用户的处理选择。</summary>
    public event Action<UnsavedChangesChoice>? Closed;

    public string Title { get; }

    public string Message { get; }

    public string SaveText { get; }

    public string DiscardText { get; }

    public string CancelText { get; }

    public ICommand SaveCommand { get; }

    public ICommand DiscardCommand { get; }

    public ICommand CancelCommand { get; }

    public UnsavedChangesDialogViewModel(string title, string message, ILocalizationService localization)
    {
        Title = title;
        Message = message;
        SaveText = localization.GetString("Dialog_Save");
        DiscardText = localization.GetString("Dialog_Discard");
        CancelText = localization.GetString("Common_Cancel");

        SaveCommand = new RelayCommand(_ => Closed?.Invoke(UnsavedChangesChoice.Save));
        DiscardCommand = new RelayCommand(_ => Closed?.Invoke(UnsavedChangesChoice.Discard));
        CancelCommand = new RelayCommand(_ => Closed?.Invoke(UnsavedChangesChoice.Cancel));
    }
}
