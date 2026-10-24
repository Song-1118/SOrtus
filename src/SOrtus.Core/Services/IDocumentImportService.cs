namespace SOrtus.Core.Services;

/// <summary>
/// 导入结果（标题 / 原文件名 / 格式 / 正文 / 提示 / 是否截断）。
/// </summary>
public sealed record ImportedDocument(
    string Title,
    string SourceFileName,
    string Format,
    string Content,
    IReadOnlyList<string> Warnings,
    bool IsTruncated);

/// <summary>
/// 项目文档导入服务（支持 Markdown / 纯文本 / docx / 旧版 doc）。
/// </summary>
public interface IDocumentImportService
{
    /// <summary>扩展名是否受支持（.md / .markdown / .txt / .docx / .doc，不区分大小写）。</summary>
    bool CanImport(string filePath);

    /// <summary>读取文件并转为可存储的正文；失败抛 <see cref="DocumentImportException"/>。</summary>
    Task<ImportedDocument> ImportAsync(string filePath);
}

/// <summary>
/// 文档导入失败。<see cref="UserMessage"/> 已是可直接展示的文案，界面不再二次包装。
/// </summary>
public sealed class DocumentImportException : Exception
{
    public DocumentImportException(string userMessage, Exception? innerException = null)
        : base(userMessage, innerException)
    {
        UserMessage = userMessage;
    }

    /// <summary>可直接展示给用户的失败原因。</summary>
    public string UserMessage { get; }
}
