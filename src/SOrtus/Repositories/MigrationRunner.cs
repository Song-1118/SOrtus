using System.IO;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace SOrtus.Repositories;

/// <summary>
/// 数据库迁移运行器（规范 9.8）。
/// 启动时读取嵌入的 vNNNN_*.sql 迁移脚本，按未应用版本顺序执行，并记录到 SchemaVersion 表。
/// 脚本幂等（CREATE TABLE IF NOT EXISTS），可重复执行不报错。
/// </summary>
internal static class MigrationRunner
{
    private const string ResourcePrefix = "SOrtus.Migrations.";

    /// <summary>执行所有待应用的迁移；首次运行会创建库与基表。</summary>
    public static async Task RunAsync()
    {
        Database.EnsureDirectory();
        await QuarantineIncompatibleDatabaseAsync();

        using var connection = new SqliteConnection(Database.ConnectionString);
        await connection.OpenAsync();

        await EnsureVersionTableAsync(connection);
        var applied = await GetAppliedVersionsAsync(connection);
        var migrations = LoadMigrations();

        foreach (var migration in migrations.OrderBy(m => m.Version))
        {
            if (applied.Contains(migration.Version))
            {
                continue;
            }

            await ExecuteScriptAsync(connection, migration.Sql);
            await RecordVersionAsync(connection, migration.Version);
        }
    }

    /// <summary>
    /// 兼容性守卫：若磁盘上的数据库来自不兼容的早期结构（开发期迭代遗留），
    /// 就地备份为 <c>*.legacy-时间戳.bak</c> 后重建，避免应用在错误结构上读写导致崩溃或数据错乱。
    /// 兼容结构则原样保留（不触碰用户数据）。
    /// </summary>
    private static async Task QuarantineIncompatibleDatabaseAsync()
    {
        if (!File.Exists(Database.CurrentDbPath))
        {
            return;
        }

        if (await IsSchemaCompatibleAsync())
        {
            return;
        }

        SqliteConnection.ClearAllPools();

        var backup = $"{Database.CurrentDbPath}.legacy-{DateTime.Now:yyyyMMddHHmmss}.bak";
        File.Move(Database.CurrentDbPath, backup, overwrite: true);
        foreach (var sidecar in new[] { "-wal", "-shm" })
        {
            var path = Database.CurrentDbPath + sidecar;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        Console.WriteLine($"[MIGRATION] 检测到不兼容的历史数据库结构，已备份为 {backup} 并重建。");
    }

    private static async Task<bool> IsSchemaCompatibleAsync()
    {
        await using var connection = new SqliteConnection(Database.ConnectionString);
        await connection.OpenAsync();

        // 立项表：若已存在，必须包含 MVP 所需列（缺列说明是旧结构）
        var projectColumns = await GetColumnsAsync(connection, "Project");
        if (projectColumns.Count > 0 &&
            (!projectColumns.Contains("TechStack") || !projectColumns.Contains("ProgressPercent")))
        {
            return false;
        }

        // 版本表：必须是 (Version INTEGER PRIMARY KEY, AppliedAt TEXT) 结构
        var versionColumns = await GetColumnsAsync(connection, "SchemaVersion");
        if (versionColumns.Count > 0 &&
            (versionColumns.Contains("Id") || !versionColumns.Contains("Version") || !versionColumns.Contains("AppliedAt")))
        {
            return false;
        }

        return true;
    }

    private static async Task<HashSet<string>> GetColumnsAsync(SqliteConnection connection, string table)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = new SqliteCommand($"SELECT name FROM pragma_table_info('{table}');", connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static async Task EnsureVersionTableAsync(SqliteConnection connection)
    {
        const string sql = "CREATE TABLE IF NOT EXISTS SchemaVersion (Version INTEGER PRIMARY KEY, AppliedAt TEXT NOT NULL);";
        await using var command = new SqliteCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<HashSet<int>> GetAppliedVersionsAsync(SqliteConnection connection)
    {
        const string sql = "SELECT Version FROM SchemaVersion;";
        await using var command = new SqliteCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var versions = new HashSet<int>();
        while (await reader.ReadAsync())
        {
            versions.Add(reader.GetInt32(0));
        }

        return versions;
    }

    private static async Task ExecuteScriptAsync(SqliteConnection connection, string sql)
    {
        // SQLite 支持以分号分隔的多语句；逐条执行更稳妥（脚本内不含字符串内的分号）。
        await using var transaction = connection.BeginTransaction();
        foreach (var statement in sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await using var command = new SqliteCommand(statement, connection, transaction);
            await command.ExecuteNonQueryAsync();
        }

        transaction.Commit();
    }

    private static async Task RecordVersionAsync(SqliteConnection connection, int version)
    {
        const string sql = "INSERT INTO SchemaVersion (Version, AppliedAt) VALUES ($version, $appliedAt);";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$version", version);
        command.Parameters.AddWithValue("$appliedAt", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    private static IReadOnlyList<(int Version, string Sql)> LoadMigrations()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var result = new List<(int Version, string Sql)>();

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal) || !name.EndsWith(".sql", StringComparison.Ordinal))
            {
                continue;
            }

            var fileName = name[ResourcePrefix.Length..];
            var versionPart = fileName.Split('_', StringSplitOptions.RemoveEmptyEntries)[0];
            if (!int.TryParse(versionPart.TrimStart('v'), out var version))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            result.Add((version, reader.ReadToEnd()));
        }

        return result;
    }
}
