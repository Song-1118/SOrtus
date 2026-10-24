using System.ComponentModel;
using System.Windows;
using SOrtus.ViewModels;

namespace SOrtus;

/// <summary>
/// 主窗口外壳（规范：主窗口骨架与基础导航框架）。
/// 内容区通过 ViewTemplates.xaml 将视图模型映射为对应视图。
/// 关闭窗口时若当前详情页有未保存更改，先走「保存 / 不保存 / 取消」确认（验收 A12）。
/// </summary>
public partial class MainWindow : Window
{
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_closeConfirmed || DataContext is not MainViewModel vm || !vm.HasUnsavedChanges)
        {
            base.OnClosing(e);
            return;
        }

        // 先拦下这次关闭，等用户在覆盖层里做完选择（异步）后再决定是否真正关闭
        e.Cancel = true;
        _ = ConfirmCloseAsync(vm);
    }

    private async Task ConfirmCloseAsync(MainViewModel vm)
    {
        if (!await vm.CanCloseAsync())
        {
            return;
        }

        _closeConfirmed = true;
        Close();
    }
}
