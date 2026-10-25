using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SOrtus.Common;

/// <summary>
/// MVVM 视图模型基类（规范：分层与可测试性）。
/// 提供属性通知与异步操作状态（IsBusy）支持。
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    private bool _isBusy;

    /// <summary>是否正在执行异步操作（用于 UI 繁忙态）。</summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>执行异步操作并自动管理 IsBusy 状态。</summary>
    protected async Task RunBusyAsync(Func<Task> work)
    {
        IsBusy = true;
        try
        {
            await work();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 语言切换后的刷新钩子（VD-110）。
    /// <para>
    /// XAML 侧的 <c>{DynamicResource}</c> 会自动换成新语言，但视图模型里**拼好**的字符串不会：
    /// 副标题、数据库位置、版本行、下拉项显示名都是在 get 里 format 一次就交给绑定。
    /// 于是切到英文后会出现「Settings」+「共 3 个立项」这种半中半英。
    /// 默认实现发一次「全部属性已变更」（空属性名），让所有绑定重新取值；
    /// 需要在切换时重建集合的视图模型（如设置页的选项列表）再重写本方法。
    /// </para>
    /// </summary>
    public virtual void OnCultureChanged() => OnPropertyChanged(string.Empty);
}
