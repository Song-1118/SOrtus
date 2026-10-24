using SOrtus.Common;

namespace SOrtus.Navigation;

/// <summary>
/// 导航服务契约：主窗口内容区在不同视图模型之间切换（规范：简单内容区切换，不引入重型框架）。
/// </summary>
public interface INavigationService
{
    /// <summary>当前显示的视图模型。</summary>
    ViewModelBase Current { get; }

    /// <summary>导航到指定视图模型。</summary>
    void NavigateTo(ViewModelBase viewModel);

    /// <summary>导航变更事件。</summary>
    event EventHandler<ViewModelBase>? Navigated;
}
