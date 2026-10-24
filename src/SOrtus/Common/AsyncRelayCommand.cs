using System.Windows.Input;

namespace SOrtus.Common;

/// <summary>
/// 轻量异步命令（规范：异步方法使用 async/await，禁止阻塞）。
/// 执行期间自动禁用（CanExecute=false）以避免重入。
/// </summary>
public sealed class AsyncRelayCommand : ICommand, IDisposable
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private int _isRunning;

    public AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute())
    {
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
    {
        // 注意：CanExecute 可能被 WPF 高频调用，必须无副作用；早期实现里在此处把运行标志置 1，
        // 导致 Execute 判定「已在运行」直接返回，异步命令永远不会执行。
        return Volatile.Read(ref _isRunning) == 0 && (_canExecute?.Invoke(parameter) ?? true);
    }

    public void Execute(object? parameter) => _ = ExecuteAsync(parameter);

    /// <summary>可直接 await 的执行入口（便于测试与需要串行等待的场景）。</summary>
    public async Task ExecuteAsync(object? parameter = null)
    {
        if (Interlocked.Exchange(ref _isRunning, 1) != 0)
        {
            return;
        }

        CommandManager.InvalidateRequerySuggested();
        try
        {
            await _execute(parameter);
        }
        finally
        {
            Interlocked.Exchange(ref _isRunning, 0);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
