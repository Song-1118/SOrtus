using System.Globalization;
using Microsoft.Data.Sqlite;
using SOrtus.Core.Models;
using SOrtus.Core.Repositories;

namespace SOrtus.Repositories;

/// <summary>
/// 项目文档仓储实现（数据访问层）。基于 Microsoft.Data.Sqlite 手写 SQL，负责读写与映射。
/// 不在此处编写业务规则（规范 2.2 分层职责）。
/// </summary>
internal sealed class ProjectDocumentRepository : IProjectDocumentRepository
{
    private const string SelectColumns =
        "Id, ProjectId, Category, Title, SourceFileName, Format, Content, SortOrder, CreatedAt, UpdatedAt";

    public async Task<IReadOnlyList<ProjectDocument>> GetByProjectAsync(int projectId)
    {
        var sql = $"SELECT {SelectColumns} FROM ProjectDocument WHERE ProjectId = $projectId ORDER BY SortOrder, Id;";
        var result = new List<ProjectDocument>();

        await using var connection = new SqliteConnection(Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$projectId", projectId);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(ReadDocument(reader));
        }

        return result;
    }

    public async Task<ProjectDocument?> GetSlotAsync(int projectId, DocumentCategory category)
    {
        var sql = $"SELECT {SelectColumns} FROM ProjectDocument WHERE ProjectId = $projectId AND Category = $category LIMIT 1;";
        await using var connection = new SqliteConnection(Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$projectId", projectId);
        command.Parameters.AddWithValue("$category", category.ToString());

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadDocument(reader) : null;
    }

    public async Task<int> UpsertAsync(ProjectDocument document)
    {
        await using var connection = new SqliteConnection(Database.ConnectionString);
        await connection.OpenAsync();

        var affected = 0;

        if (document.Id > 0)
        {
            affected = await UpdateAsync(connection, document);
        }
        else if (IsSingletonCategory(document.Category))
        {
            // 单槽位分类：Id 尚未确定时先按 (ProjectId, Category) 查现存槽位，命中则更新。
            var slotId = await FindSlotIdAsync(connection, document.ProjectId, document.Category);
            if (slotId > 0)
            {
                document.Id = slotId;
                affected = await UpdateAsync(connection, document);
            }
        }

        if (affected > 0)
        {
            return document.Id;
        }

        try
        {
            return await InsertAsync(connection, document);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            // 唯一约束兜底：并发/竞态下槽位已被占用，改为更新既有记录，避免上层重试失败。
            var slotId = await FindSlotIdAsync(connection, document.ProjectId, document.Category);
            if (slotId <= 0)
            {
                throw;
            }

            document.Id = slotId;
            await UpdateAsync(connection, document);
            return slotId;
        }
    }

    public async Task<bool> UpdateContentAsync(int documentId, string content)
    {
        const string sql = "UPDATE ProjectDocument SET Content = $content, UpdatedAt = $updatedAt WHERE Id = $id;";
        await using var connection = new SqliteConnection(Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$content", content ?? string.Empty);
        command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", documentId);

        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteAsync(int documentId)
    {
        const string sql = "DELETE FROM ProjectDocument WHERE Id = $id;";
        await using var connection = new SqliteConnection(Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$id", documentId);

        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<int> GetNextSortOrderAsync(int projectId)
    {
        const string sql = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM ProjectDocument WHERE ProjectId = $projectId;";
        await using var connection = new SqliteConnection(Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$projectId", projectId);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is null or DBNull ? 0 : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
    }

    private static async Task<int> UpdateAsync(SqliteConnection connection, ProjectDocument document)
    {
        const string sql = """
            UPDATE ProjectDocument SET
                ProjectId = $projectId, Category = $category, Title = $title,
                SourceFileName = $sourceFileName, Format = $format, Content = $content,
                SortOrder = $sortOrder, UpdatedAt = $updatedAt
            WHERE Id = $id;
            """;
        await using var command = new SqliteCommand(sql, connection);
        BindParameters(command, document);
        command.Parameters.AddWithValue("$id", document.Id);

        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> InsertAsync(SqliteConnection connection, ProjectDocument document)
    {
        const string sql = """
            INSERT INTO ProjectDocument
                (ProjectId, Category, Title, SourceFileName, Format, Content, SortOrder, CreatedAt, UpdatedAt)
            VALUES
                ($projectId, $category, $title, $sourceFileName, $format, $content, $sortOrder, $createdAt, $updatedAt);
            SELECT last_insert_rowid();
            """;
        await using var command = new SqliteCommand(sql, connection);
        BindParameters(command, document);
        command.Parameters.AddWithValue("$createdAt", document.CreatedAt.ToString("O"));

        var id = await command.ExecuteScalarAsync();
        document.Id = Convert.ToInt32(id, CultureInfo.InvariantCulture);
        return document.Id;
    }

    private static async Task<int> FindSlotIdAsync(SqliteConnection connection, int projectId, DocumentCategory category)
    {
        const string sql = "SELECT Id FROM ProjectDocument WHERE ProjectId = $projectId AND Category = $category LIMIT 1;";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$projectId", projectId);
        command.Parameters.AddWithValue("$category", category.ToString());

        var scalar = await command.ExecuteScalarAsync();
        return scalar is null or DBNull ? 0 : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
    }

    private static ProjectDocument ReadDocument(SqliteDataReader reader)
    {
        return new ProjectDocument
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            ProjectId = reader.GetInt32(reader.GetOrdinal("ProjectId")),
            Category = ParseCategory(reader.GetString(reader.GetOrdinal("Category"))),
            Title = reader.GetString(reader.GetOrdinal("Title")),
            SourceFileName = reader.GetString(reader.GetOrdinal("SourceFileName")),
            Format = reader.GetString(reader.GetOrdinal("Format")),
            Content = reader.GetString(reader.GetOrdinal("Content")),
            SortOrder = reader.GetInt32(reader.GetOrdinal("SortOrder")),
            CreatedAt = ReadDateTime(reader, "CreatedAt"),
            UpdatedAt = ReadDateTime(reader, "UpdatedAt"),
        };
    }

    /// <summary>分类存储为 TEXT；无法解析的值回落为 <see cref="DocumentCategory.Other"/>，不抛异常。</summary>
    private static DocumentCategory ParseCategory(string value) =>
        Enum.TryParse<DocumentCategory>(value, ignoreCase: true, out var parsed)
            ? parsed
            : DocumentCategory.Other;

    /// <summary>时间以 ISO-8601（"O"）存储；空值或非法值回落为 <see cref="DateTime.UtcNow"/>。</summary>
    private static DateTime ReadDateTime(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return DateTime.UtcNow;
        }

        var raw = reader.GetString(ordinal);
        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : DateTime.UtcNow;
    }

    private static void BindParameters(SqliteCommand command, ProjectDocument document)
    {
        command.Parameters.AddWithValue("$projectId", document.ProjectId);
        command.Parameters.AddWithValue("$category", document.Category.ToString());
        command.Parameters.AddWithValue("$title", document.Title ?? string.Empty);
        command.Parameters.AddWithValue("$sourceFileName", document.SourceFileName ?? string.Empty);
        command.Parameters.AddWithValue("$format", document.Format ?? "md");
        command.Parameters.AddWithValue("$content", document.Content ?? string.Empty);
        command.Parameters.AddWithValue("$sortOrder", document.SortOrder);
        command.Parameters.AddWithValue("$updatedAt", document.UpdatedAt.ToString("O"));
    }

    /// <summary>Prd / IterationPlan 为「单槽位」分类，同一立项下只允许存在一条记录。</summary>
    private static bool IsSingletonCategory(DocumentCategory category) =>
        category is DocumentCategory.Prd or DocumentCategory.IterationPlan;
}
