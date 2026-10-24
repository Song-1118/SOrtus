using System.IO;
using Microsoft.Data.Sqlite;

namespace SOrtus.Repositories;

/// <summary>
/// SQLite 连接与路径管理（数据访问层基础设施）。
/// 数据库文件位于 %LocalAppData%/SOrtus/sortus.db，纯本地、运行期生成。
/// </summary>
internal static class Database
{
    /// <summary>数据库文件名（修正早期版本的 soctus 拼写；旧文件在首次启动时自动改名保留数据）。</summary>
    private const string DbFileName = "sortus.db";

    /// <summary>历史版本使用的文件名（仅用于一次性改名迁移，向后兼容旧数据）。</summary>
    private const string LegacyDbFileName = "soctus.db";

    private static readonly string DefaultDbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SOrtus",
        DbFileName);

    /// <summary>测试可临时覆盖数据库路径（默认落于 %LocalAppData%/SOrtus/sortus.db）。</summary>
    internal static string? OverrideDbPath { get; set; }

    private static string DbPath => OverrideDbPath ?? DefaultDbPath;

    /// <summary>当前实际使用的数据库文件路径（设置页展示 / 测试断言用）。</summary>
    public static string CurrentDbPath => DbPath;

    /// <summary>SQLite 连接字符串（不存在则创建）。</summary>
    public static string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = DbPath,
        Mode = SqliteOpenMode.ReadWriteCreate,
    }.ToString();

    /// <summary>确保数据库所在目录存在，并把历史文件名的旧库改名到新文件名（不丢数据）。</summary>
    public static void EnsureDirectory()
    {
        var dir = Path.GetDirectoryName(DbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        TryMigrateLegacyFile(dir);
    }

    private static void TryMigrateLegacyFile(string? dir)
    {
        if (OverrideDbPath is not null || string.IsNullOrEmpty(dir) || File.Exists(DbPath))
        {
            return;
        }

        var legacy = Path.Combine(dir, LegacyDbFileName);
        if (!File.Exists(legacy))
        {
            return;
        }

        try
        {
            File.Move(legacy, DbPath);
        }
        catch (IOException)
        {
            // 改名失败（占用 / 权限）不阻断启动：继续以新文件工作，旧文件保留待用户手动处理。
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
