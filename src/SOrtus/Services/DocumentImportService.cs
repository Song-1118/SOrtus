using System.IO;
using System.Text;
using SOrtus.Core.Services;

namespace SOrtus.Services;

/// <summary>
/// 项目文档导入服务（规范：详情页 V2「开发管理」文档导入）。
/// <para>
/// 支持 Markdown / 纯文本 / docx / 旧版 doc。docx 走 <see cref="DocxTextExtractor"/>；
/// doc 为二进制 OLE 格式，无零依赖可靠解析方案，这里做「尽力而为」提取：
/// 分别按 UTF-16LE 与 CP936 解码后取可打印字符占比更高的一路，占比过低直接失败并提示另存为 docx。
/// </para>
/// </summary>
internal sealed class DocumentImportService : IDocumentImportService
{
    /// <summary>单文件读取上限（2 MB）：超出只读前 2 MB 并标记截断，避免超大文档拖垮 UI。</summary>
    private const int MaxBytes = 2 * 1024 * 1024;

    private const string UnsupportedExtension = "不支持的文件类型";

    private static readonly string[] SupportedExtensions = [".md", ".markdown", ".txt", ".docx", ".doc"];

    static DocumentImportService()
    {
        // CP936（GB18030）需要注册代码页提供程序；失败不影响 UTF-8 路径。
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        catch (ArgumentException)
        {
            // 已注册或运行时不支持，忽略。
        }
    }

    public bool CanImport(string filePath) =>
        !string.IsNullOrWhiteSpace(filePath)
        && SupportedExtensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase);

    public async Task<ImportedDocument> ImportAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new DocumentImportException("文件不存在或已被移动");
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (!CanImport(filePath))
        {
            throw new DocumentImportException(UnsupportedExtension);
        }

        byte[] bytes;
        try
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var length = (int)Math.Min(stream.Length, MaxBytes);
            bytes = new byte[length];
            var read = 0;
            while (read < length)
            {
                var chunk = await stream.ReadAsync(bytes.AsMemory(read, length - read));
                if (chunk <= 0)
                {
                    break;
                }

                read += chunk;
            }

            if (read < length)
            {
                Array.Resize(ref bytes, read);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new DocumentImportException("没有读取该文件的权限", ex);
        }
        catch (IOException ex)
        {
            throw new DocumentImportException("文件读取失败，可能正被其他程序占用", ex);
        }

        var truncated = new FileInfo(filePath).Length > MaxBytes;
        var warnings = new List<string>();
        if (truncated)
        {
            warnings.Add("文件较大，仅导入前 2 MB 内容");
        }

        var title = Path.GetFileNameWithoutExtension(filePath);
        var fileName = Path.GetFileName(filePath);

        switch (extension)
        {
            case ".md":
            case ".markdown":
            {
                var content = ReadText(bytes);
                return new ImportedDocument(title, fileName, "md", content, warnings, truncated);
            }

            case ".txt":
            {
                var content = ReadText(bytes);
                return new ImportedDocument(title, fileName, "txt", content, warnings, truncated);
            }

            case ".docx":
            {
                using var memory = new MemoryStream(bytes);
                var (text, skippedImages) = DocxTextExtractor.Extract(memory);
                if (skippedImages > 0)
                {
                    warnings.Add($"已忽略 {skippedImages} 张图片");
                }

                return new ImportedDocument(title, fileName, "docx", text, warnings, truncated);
            }

            case ".doc":
            {
                var text = ExtractLegacyDoc(bytes);
                warnings.Add("旧版 .doc 为尽力提取，格式可能失真");
                return new ImportedDocument(title, fileName, "doc", text, warnings, truncated);
            }

            default:
                throw new DocumentImportException(UnsupportedExtension);
        }
    }

    /// <summary>按 BOM / UTF-8 严格解码 / GB18030 回退的顺序读取文本。</summary>
    private static string ReadText(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            var cp936 = TryGetEncoding(936);
            return cp936 is null ? Encoding.Latin1.GetString(bytes) : cp936.GetString(bytes);
        }
    }

    /// <summary>
    /// 旧版 .doc 尽力提取：OLE 容器里的正文多为 UTF-16LE，也可能被存成 ANSI（CP936）。
    /// 两路都解一遍，取「可打印字符占比」更高且内容更长的结果。
    /// </summary>
    private static string ExtractLegacyDoc(byte[] bytes)
    {
        var utf16 = DecodeCandidate(bytes, Encoding.Unicode);
        var cp936Encoding = TryGetEncoding(936);
        var ansi = cp936Encoding is null ? (Text: string.Empty, Ratio: 0d) : DecodeCandidate(bytes, cp936Encoding);

        var best = utf16.Ratio >= ansi.Ratio ? utf16 : ansi;
        if (best.Text.Length < 10 || best.Ratio < 0.7 || !HasReadableSeparators(best.Text))
        {
            throw new DocumentImportException("旧版 .doc 无法可靠解析，请在 Word 中另存为 .docx 后重新导入");
        }

        return best.Text;
    }

    /// <summary>
    /// 可读性守卫：真实文档必有空格 / 换行 / 中文标点等分隔符；
    /// 二进制被强行解码成「全是字母」的乱码时通常没有这些分隔符，据此拒绝入库。
    /// </summary>
    private static bool HasReadableSeparators(string text) =>
        text.Contains(' ')
        || text.Contains('\n')
        || text.Contains('\t')
        || text.Any(ch => "，。、；：！？“”（）《》".Contains(ch));

    private static (string Text, double Ratio) DecodeCandidate(byte[] bytes, Encoding encoding)
    {
        string raw;
        try
        {
            raw = encoding.GetString(bytes);
        }
        catch (ArgumentException)
        {
            return (string.Empty, 0);
        }

        var builder = new StringBuilder(raw.Length);
        var printable = 0;
        var total = 0;

        foreach (var ch in raw)
        {
            if (ch is '\r' or '\n' or '\t')
            {
                builder.Append(ch);
                continue;
            }

            if (ch < 0x20 || ch == 0x7F)
            {
                // 控制字符：常见于二进制头部，直接丢弃（不计入可打印率分母之外）。
                continue;
            }

            total++;
            if (IsPrintable(ch))
            {
                printable++;
                builder.Append(ch);
            }
        }

        var text = Normalize(builder.ToString());
        return total == 0 ? (text, 0) : (text, (double)printable / total);
    }

    /// <summary>可打印判定：中日韩文字、常见符号、字母数字。</summary>
    private static bool IsPrintable(char ch) =>
        char.IsLetterOrDigit(ch)
        || char.IsPunctuation(ch)
        || char.IsSymbol(ch)
        || (ch >= 0x4E00 && ch <= 0x9FFF);

    private static string Normalize(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var builder = new StringBuilder();
        var blankStreak = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                blankStreak++;
                if (blankStreak > 1)
                {
                    continue;
                }
            }
            else
            {
                blankStreak = 0;
            }

            builder.Append(trimmed).Append('\n');
        }

        return builder.ToString().Trim();
    }

    private static Encoding? TryGetEncoding(int codePage)
    {
        try
        {
            return Encoding.GetEncoding(codePage);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
