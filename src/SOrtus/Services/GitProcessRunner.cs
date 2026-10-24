using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using SOrtus.Core.Services;

namespace SOrtus.Services;

/// <summary>
/// git 命令行执行器（规范 1.5：不引入 LibGit2Sharp，直接调用本机 git）。
/// <para>
/// 关键防护：清空凭据助手 + 关闭终端交互提示（否则认证失败会弹出凭据窗口把进程挂死），
/// 统一 UTF-8 输出避免中文提交信息乱码，超时后杀掉整棵进程树。
/// 任何异常都转成结果对象，绝不向调用方抛出。
/// </para>
/// </summary>
internal sealed class GitProcessRunner : IGitCommandRunner
{
    public async Task<GitCommandResult> RunAsync(string workDir, IReadOnlyList<string> args, TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // 中文路径不着色、不转义；凭据助手置空 + 关闭交互提示，认证失败立即返回
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("core.quotepath=false");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("credential.helper=");

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GCM_INTERACTIVE"] = "never";
        startInfo.Environment["LC_ALL"] = "C.UTF-8";

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(timeout);
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return new GitCommandResult(-1, await SafeReadAsync(stdoutTask), await SafeReadAsync(stderrTask), true);
            }

            return new GitCommandResult(
                process.ExitCode,
                await SafeReadAsync(stdoutTask),
                await SafeReadAsync(stderrTask),
                false);
        }
        catch (Win32Exception)
        {
            // git 不在 PATH：不是异常路径，交给上层提示「未检测到 git」
            return new GitCommandResult(-2, string.Empty, "GIT_NOT_INSTALLED", false);
        }
        catch (Exception ex)
        {
            return new GitCommandResult(-3, string.Empty, ex.Message, false);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // 进程已退出
        }
        catch (Win32Exception)
        {
            // 无权结束，忽略
        }
    }

    private static async Task<string> SafeReadAsync(Task<string> task)
    {
        try
        {
            return await task;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
