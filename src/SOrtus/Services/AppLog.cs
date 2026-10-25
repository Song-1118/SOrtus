using System.IO;

namespace SOrtus.Services;

/// <summary>
/// 应用日志（VD-119）：技术细节落盘，不进用户可见的主文案。
/// <para>
/// 文件位于 %LocalAppData%\SOrtus\logs\app.log，按天滚动（超过 2MB 就换成 .old）。
/// 失败本身绝不抛出——日志坏了不应该再叠加一个错误。
/// </para>
/// </summary>
internal static class AppLog
{
    private static readonly object Gate = new();
    private const long MaxBytes = 2 * 1024 * 1024;

    public static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SOrtus",
        "logs");

    public static string LogPath => Path.Combine(LogDirectory, "app.log");

    /// <summary>写一条错误日志（scope 说明是哪一段代码，ex 是原始异常）。</summary>
    public static void Write(string scope, Exception ex)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(LogDirectory);

                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxBytes)
                {
                    File.Move(LogPath, Path.Combine(LogDirectory, "app.old"), overwrite: true);
                }

                File.AppendAllText(
                    LogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {scope}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch
        {
            // 日志不可用不应影响主流程
        }
    }

    /// <summary>写一条普通信息（可选，用于难以复现的分支）。</summary>
    public static void Info(string scope, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(
                    LogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {scope}: {message}{Environment.NewLine}");
            }
        }
        catch
        {
        }
    }
}
