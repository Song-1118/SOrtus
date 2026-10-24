using System.Windows;
using System.Windows.Controls;
using SOrtus.Core.Services;

namespace SOrtus.Services;

/// <summary>
/// 对话框服务实现：主窗口内覆盖层承载（规范 7.7 红线 #5）。
/// <para>
/// 视图模型通过 <see cref="IDialogService"/> 发起对话框；本服务把对话框视图模型
/// 通过 <see cref="ActiveDialogChanged"/> 事件交给主窗口覆盖层显示，并用
/// TaskCompletionSource 等待用户操作结果。无宿主时降级为 MessageBox（保证可用）。
/// </para>
/// </summary>
internal sealed class DialogService : IDialogService
{
    private readonly ILocalizationService _localization;

    /// <summary>当前对话框视图模型变化（null 表示关闭）。仅在 UI 线程触发。</summary>
    public event Action<object?>? ActiveDialogChanged;

    public DialogService(ILocalizationService localization)
    {
        _localization = localization;
    }

    public Task<bool> ConfirmAsync(string title, string message) =>
        ShowAsync(new ViewModels.ConfirmDialogViewModel(title, message, isConfirm: true, _localization));

    public Task<ViewModels.UnsavedChangesChoice> ConfirmUnsavedAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource<ViewModels.UnsavedChangesChoice>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new ViewModels.UnsavedChangesDialogViewModel(title, message, _localization);

        vm.Closed += choice =>
        {
            SetActiveDialog(null);
            tcs.TrySetResult(choice);
        };

        SetActiveDialog(vm);
        return tcs.Task;
    }

    public Task InfoAsync(string title, string message) =>
        ShowAsync(new ViewModels.ConfirmDialogViewModel(title, message, isConfirm: false, _localization));

    public Task ShowAsync(ViewModels.IOverlayDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnClosed()
        {
            viewModel.Closed -= OnClosed;
            SetActiveDialog(null);
            tcs.TrySetResult();
        }

        viewModel.Closed += OnClosed;
        SetActiveDialog(viewModel);
        return tcs.Task;
    }

    private Task<bool> ShowAsync(ViewModels.ConfirmDialogViewModel vm)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        vm.Closed += result =>
        {
            SetActiveDialog(null);
            tcs.TrySetResult(result);
        };

        SetActiveDialog(vm);
        return tcs.Task;
    }

    private void SetActiveDialog(object? content)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.InvokeAsync(() => ActiveDialogChanged?.Invoke(content));
        }
        else
        {
            ActiveDialogChanged?.Invoke(content);
        }
    }
}
