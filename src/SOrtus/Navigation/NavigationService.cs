using SOrtus.Common;

namespace SOrtus.Navigation;

/// <summary>
/// 导航服务默认实现，承载当前视图模型并广播导航事件。
/// 视图通过 DataTemplate 将视图模型映射到对应 XAML 视图（见 Resources/ViewTemplates.xaml）。
/// </summary>
internal sealed class NavigationService : INavigationService
{
    private ViewModelBase _current = null!;

    public ViewModelBase Current
    {
        get => _current;
        private set => _current = value;
    }

    public event EventHandler<ViewModelBase>? Navigated;

    public void NavigateTo(ViewModelBase viewModel)
    {
        Current = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        Navigated?.Invoke(this, viewModel);
    }
}
