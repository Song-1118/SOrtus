using SOrtus.Common;
using Xunit;

namespace SOrtus.UnitTests;

/// <summary>
/// 轻量异步命令测试（回归：命令在被 WPF 查询过 CanExecute 之后仍必须可执行）。
/// </summary>
public class AsyncRelayCommandTests
{
    [Fact]
    public async Task Execute_RunsAfterCanExecuteQueried()
    {
        var runs = 0;
        var command = new AsyncRelayCommand(() =>
        {
            runs++;
            return Task.CompletedTask;
        });

        // WPF 会在按钮渲染时高频查询 CanExecute；查询不得产生副作用
        Assert.True(command.CanExecute(null));
        Assert.True(command.CanExecute(null));

        command.Execute(null);
        await WaitUntilAsync(() => runs == 1);

        Assert.Equal(1, runs);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task Execute_IsNotReentrant_WhileRunning()
    {
        var gate = new TaskCompletionSource();
        var started = 0;
        var command = new AsyncRelayCommand(async () =>
        {
            started++;
            await gate.Task;
        });

        command.Execute(null);
        await WaitUntilAsync(() => started == 1);

        // 运行中：CanExecute 应为 false，重复执行被忽略
        Assert.False(command.CanExecute(null));
        command.Execute(null);
        Assert.Equal(1, started);

        gate.SetResult();
        await WaitUntilAsync(() => command.CanExecute(null));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }
}
