namespace SOrtus.Services;

/// <summary>
/// 对话框服务契约（规范 7.7 红线 #5：不用 AllowsTransparency 全透明窗口，
/// 统一走主窗口内覆盖层 + 磨砂卡片；替代 MessageBox，保持视觉一致）。
/// </summary>
public interface IDialogService
{
    /// <summary>当前对话框视图模型变化（null 表示关闭）。仅在 UI 线程触发。</summary>
    event Action<object?>? ActiveDialogChanged;

    /// <summary>确认对话框（确认 / 取消），返回用户是否确认。</summary>
    Task<bool> ConfirmAsync(string title, string message);

    /// <summary>
    /// 破坏性操作的确认对话框（VD-117）：主按钮用危险色、默认焦点落在「取消」，
    /// 只用于「删了就回不来」的动作（删除立项 / 删除文档），不用于「覆盖导入」这类可恢复操作。
    /// </summary>
    Task<bool> ConfirmDestructiveAsync(string title, string message);

    /// <summary>未保存更改三选对话框（保存 / 不保存 / 取消），返回用户选择（验收 A12）。</summary>
    Task<ViewModels.UnsavedChangesChoice> ConfirmUnsavedAsync(string title, string message);

    /// <summary>提示对话框（仅确定），返回 true。</summary>
    Task InfoAsync(string title, string message);

    /// <summary>
    /// 展示任意覆盖层对话框（文档预览 / 配置远端等），任务在对话框关闭时完成。
    /// 视图由 ViewTemplates.xaml 中对应视图模型的 DataTemplate 提供。
    /// </summary>
    Task ShowAsync(ViewModels.IOverlayDialogViewModel viewModel);
}
