using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using SOrtus.Core.Services;

namespace SOrtus.Services;

/// <summary>
/// docx 文本抽取（零第三方依赖）：docx 本质是 zip，正文在 <c>word/document.xml</c>。
/// <para>
/// 按元素 LocalName 匹配，忽略 <c>w:</c> 命名空间前缀差异；只取文本与段落/表格结构，
/// 图片丢弃但计数，由调用方提示用户「已忽略 n 张图片」。
/// </para>
/// </summary>
internal static class DocxTextExtractor
{
    /// <summary>抽取正文文本；返回文本与跳过的图片数量。</summary>
    public static (string Text, int SkippedImages) Extract(Stream docxStream)
    {
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(docxStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException ex)
        {
            throw new DocumentImportException("文件不是有效的 docx 文档", ex);
        }

        using (archive)
        {
            var entry = archive.GetEntry("word/document.xml")
                        ?? throw new DocumentImportException("文件不是有效的 docx 文档");

            XDocument document;
            using (var stream = entry.Open())
            {
                try
                {
                    document = XDocument.Load(stream);
                }
                catch (System.Xml.XmlException ex)
                {
                    throw new DocumentImportException("docx 正文结构损坏，无法解析", ex);
                }
            }

            var images = document.Descendants()
                .Count(e => e.Name.LocalName is "drawing" or "pict");

            var body = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "body");
            if (body is null)
            {
                return (string.Empty, images);
            }

            var builder = new StringBuilder();
            foreach (var element in body.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "p":
                        AppendParagraph(element, builder);
                        break;
                    case "tbl":
                        AppendTable(element, builder);
                        break;
                }
            }

            return (Normalize(builder.ToString()), images);
        }
    }

    private static void AppendParagraph(XElement paragraph, StringBuilder builder)
    {
        var text = ReadInlineText(paragraph);
        if (text.Length > 0)
        {
            builder.Append(text.TrimEnd('\n')).Append('\n');
        }
        else
        {
            builder.Append('\n');
        }
    }

    private static void AppendTable(XElement table, StringBuilder builder)
    {
        foreach (var row in table.Descendants().Where(e => e.Name.LocalName == "tr"))
        {
            var cells = row.Elements()
                .Where(e => e.Name.LocalName == "tc")
                .Select(cell => ReadInlineText(cell).Replace('\n', ' ').Trim())
                .Where(cell => cell.Length > 0)
                .ToList();

            if (cells.Count > 0)
            {
                builder.Append(string.Join(" | ", cells)).Append('\n');
            }
        }
    }

    /// <summary>读取一个段落 / 单元格内的行内文本（含制表符与换行标记）。</summary>
    private static string ReadInlineText(XElement container)
    {
        var builder = new StringBuilder();
        foreach (var element in container.Descendants())
        {
            switch (element.Name.LocalName)
            {
                case "t":
                    builder.Append(element.Value);
                    break;
                case "tab":
                    builder.Append('\t');
                    break;
                case "br":
                case "cr":
                    builder.Append('\n');
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>压缩连续空行（最多保留 1 行）并去掉首尾空白。</summary>
    private static string Normalize(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var builder = new StringBuilder();
        var blankStreak = 0;

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd();
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

        return builder.ToString().Trim('\n');
    }
}
