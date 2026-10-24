using Microsoft.Data.Sqlite;
using SOrtus.Core.Models;
using SOrtus.Core.Repositories;

namespace SOrtus.Repositories;

/// <summary>
/// 立项仓储实现（数据访问层）。基于 Microsoft.Data.Sqlite，负责 SQLite 读写与映射。
/// 不在此处编写业务规则（规范 2.2 分层职责）。
/// </summary>
internal sealed class ProjectRepository : IProjectRepository
{
    public async Task<IReadOnlyList<Project>> GetAllAsync()
    {
        return await QueryAsync(null, null, null);
    }

    public async Task<IReadOnlyList<Project>> QueryAsync(ProjectType? type, ProjectStatus? status, string? keyword)
    {
        var sql = "SELECT * FROM Project WHERE 1=1";
        if (type.HasValue) sql += " AND Type = $type";
        if (status.HasValue) sql += " AND Status = $status";
        if (!string.IsNullOrWhiteSpace(keyword)) sql += " AND (Name LIKE $kw OR Summary LIKE $kw OR TechStack LIKE $kw)";
        sql += " ORDER BY UpdatedAt DESC, Id DESC;";

        var result = new List<Project>();
        await using var connection = new SqliteConnection(Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqliteCommand(sql, connection);
        if (type.HasValue) command.Parameters.AddWithValue("$type", (int)type.Value);
        if (status.HasValue) command.Parameters.AddWithValue("$status", (int)status.Value);
        if (!string.IsNullOrWhiteSpace(keyword)) command.Parameters.AddWithValue("$kw", $"%{keyword}%");

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(ReadProject(reader));
        }

        return result;
    }

    public async Task<Project?> GetByIdAsync(int id)
    {
        const string sql = "SELECT * FROM Project WHERE Id = $id;";
        await using var connection = new SqliteConnection(Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadProject(reader) : null;
    }

    public async Task<Project> AddAsync(Project project)
    {
        const string sql = """
            INSERT INTO Project
                (Name, Type, Status, CreatedAt, UpdatedAt, TechStack, Summary, SubType,
                 FolderPath, ScheduleStart, ScheduleEnd, DevPlan, Constraints, Goals, AcceptanceCriteria, ProgressPercent)
            VALUES
                ($name, $type, $status, $createdAt, $updatedAt, $techStack, $summary, $subType,
                 $folderPath, $scheduleStart, $scheduleEnd, $devPlan, $constraints, $goals, $acceptance, $progress);
            SELECT last_insert_rowid();
            """;
        await using var connection = new SqliteConnection(Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqliteCommand(sql, connection);
        BindProjectParameters(command, project, isInsert: true);
        var id = await command.ExecuteScalarAsync();
        project.Id = Convert.ToInt32(id);
        return project;
    }

    public async Task UpdateAsync(Project project)
    {
        const string sql = """
            UPDATE Project SET
                Name = $name, Type = $type, Status = $status, UpdatedAt = $updatedAt,
                TechStack = $techStack, Summary = $summary, SubType = $subType,
                FolderPath = $folderPath, ScheduleStart = $scheduleStart, ScheduleEnd = $scheduleEnd,
                DevPlan = $devPlan, Constraints = $constraints, Goals = $goals,
                AcceptanceCriteria = $acceptance, ProgressPercent = $progress
            WHERE Id = $id;
            """;
        await using var connection = new SqliteConnection(Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqliteCommand(sql, connection);
        BindProjectParameters(command, project, isInsert: false);
        command.Parameters.AddWithValue("$id", project.Id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        const string sql = "DELETE FROM Project WHERE Id = $id;";
        await using var connection = new SqliteConnection(Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }

    private static Project ReadProject(SqliteDataReader reader)
    {
        return new Project
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Type = (ProjectType)reader.GetInt32(reader.GetOrdinal("Type")),
            Status = (ProjectStatus)reader.GetInt32(reader.GetOrdinal("Status")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")), null, System.Globalization.DateTimeStyles.RoundtripKind),
            UpdatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("UpdatedAt")), null, System.Globalization.DateTimeStyles.RoundtripKind),
            TechStack = reader.GetString(reader.GetOrdinal("TechStack")),
            Summary = reader.GetString(reader.GetOrdinal("Summary")),
            SubType = reader.GetString(reader.GetOrdinal("SubType")),
            FolderPath = reader.GetString(reader.GetOrdinal("FolderPath")),
            ScheduleStart = ReadNullableDateTime(reader, "ScheduleStart"),
            ScheduleEnd = ReadNullableDateTime(reader, "ScheduleEnd"),
            DevPlan = reader.GetString(reader.GetOrdinal("DevPlan")),
            Constraints = reader.GetString(reader.GetOrdinal("Constraints")),
            Goals = reader.GetString(reader.GetOrdinal("Goals")),
            AcceptanceCriteria = reader.GetString(reader.GetOrdinal("AcceptanceCriteria")),
            ProgressPercent = reader.GetDouble(reader.GetOrdinal("ProgressPercent")),
        };
    }

    private static DateTime? ReadNullableDateTime(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : DateTime.Parse(reader.GetString(ordinal), null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    private static void BindProjectParameters(SqliteCommand command, Project project, bool isInsert)
    {
        command.Parameters.AddWithValue("$name", project.Name);
        command.Parameters.AddWithValue("$type", (int)project.Type);
        command.Parameters.AddWithValue("$status", (int)project.Status);
        if (isInsert)
        {
            command.Parameters.AddWithValue("$createdAt", project.CreatedAt.ToString("O"));
        }

        command.Parameters.AddWithValue("$updatedAt", project.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("$techStack", project.TechStack ?? string.Empty);
        command.Parameters.AddWithValue("$summary", project.Summary ?? string.Empty);
        command.Parameters.AddWithValue("$subType", project.SubType ?? string.Empty);
        command.Parameters.AddWithValue("$folderPath", project.FolderPath ?? string.Empty);
        command.Parameters.AddWithValue("$scheduleStart", ToDbValue(project.ScheduleStart?.ToString("O")));
        command.Parameters.AddWithValue("$scheduleEnd", ToDbValue(project.ScheduleEnd?.ToString("O")));
        command.Parameters.AddWithValue("$devPlan", project.DevPlan ?? string.Empty);
        command.Parameters.AddWithValue("$constraints", project.Constraints ?? string.Empty);
        command.Parameters.AddWithValue("$goals", project.Goals ?? string.Empty);
        command.Parameters.AddWithValue("$acceptance", project.AcceptanceCriteria ?? string.Empty);
        command.Parameters.AddWithValue("$progress", project.ProgressPercent);
    }

    /// <summary>
    /// 可空参数转换为数据库值。SqliteParameter.Value 为 null 时会被视作「未赋值」，
    /// 执行时抛 InvalidOperationException（Value must be set.），因此空值必须显式转为 <see cref="DBNull.Value"/>。
    /// </summary>
    private static object ToDbValue(object? value) => value ?? DBNull.Value;
}
