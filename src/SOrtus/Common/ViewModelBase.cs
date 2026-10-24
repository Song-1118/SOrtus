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
}
