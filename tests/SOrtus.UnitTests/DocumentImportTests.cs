using System.IO.Compression;
using System.Text;
using SOrtus.Core.Services;
using SOrtus.Services;
using Xunit;

namespace SOrtus.UnitTests;

/// <summary>
/// 文档导入测试（详情页 V2「开发管理」）：docx 文本抽取、Markdown / 纯文本导入、失败降级。
/// 旧版 .doc 为二进制启发式，只验证「无法可靠解析时明确失败」，不追求识别率。
/// </summary>
public class DocumentImportTests
{
    private const string DocumentXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
          <w:body>
            <w:p><w:r><w:t>第一段正文</w:t></w:r></w:p>
            <w:p><w:r><w:t>A</w:t><w:tab/><w:t>B</w:t><w:br/><w:t>C</w:t></w:r></w:p>
            <w:tbl>
              <w:tr>
                <w:tc><w:p><w:r><w:t>单元格1</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:t>单元格2</w:t></w:r></w:p></w:tc>
              </w:tr>
            </w:tbl>
            <w:p><w:r><w:drawing /></w:r></w:p>
          </w:body>
        </w:document>
        """;

    private static byte[] BuildDocx(string documentXml)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("word/document.xml");
            using var stream = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(documentXml);
            stream.Write(bytes, 0, bytes.Length);
        }

        return memory.ToArray();
    }

    private static string CreateTempFile(string fileName, byte[] bytes)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sortus_doc_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void DocxExtractor_ReadsParagraphsTabsBreaksAndTables()
    {
        using var stream = new MemoryStream(BuildDocx(DocumentXml));

        var (text, skippedImages) = DocxTextExtractor.Extract(stream);

        Assert.Contains("第一段正文", text);
        Assert.Contains("A\tB", text);
        Assert.Contains("C", text);
        Assert.Contains("单元格1 | 单元格2", text);
        Assert.Equal(1, skippedImages);
    }

    [Fact]
    public void DocxExtractor_MissingDocumentXml_Throws()
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("word/styles.xml");
        }

        memory.Position = 0;

        var exception = Assert.Throws<DocumentImportException>(() => DocxTextExtractor.Extract(memory));
        Assert.Contains("docx", exception.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportAsync_Markdown_KeepsContentAndTitle()
    {
        var content = "# 需求\n\n- [ ] 待办一\n- [x] 待办二\n";
        var path = CreateTempFile("产品需求.md", Encoding.UTF8.GetBytes(content));

        try
        {
            var service = new DocumentImportService();

            Assert.True(service.CanImport(path));
            var imported = await service.ImportAsync(path);

            Assert.Equal("产品需求", imported.Title);
            Assert.Equal("产品需求.md", imported.SourceFileName);
            Assert.Equal("md", imported.Format);
            Assert.Equal(content, imported.Content);
            Assert.False(imported.IsTruncated);
            Assert.Empty(imported.Warnings);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public async Task ImportAsync_TextWithBom_DropsBom()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(Encoding.UTF8.GetBytes("第一行\n第二行"))
            .ToArray();
        var path = CreateTempFile("笔记.txt", bytes);

        try
        {
            var imported = await new DocumentImportService().ImportAsync(path);

            Assert.StartsWith("第一行", imported.Content);
            Assert.DoesNotContain('\uFEFF', imported.Content);
            Assert.Equal("txt", imported.Format);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public async Task ImportAsync_Docx_ReportsSkippedImages()
    {
        var path = CreateTempFile("验收记录.docx", BuildDocx(DocumentXml));

        try
        {
            var imported = await new DocumentImportService().ImportAsync(path);

            Assert.Equal("docx", imported.Format);
            Assert.Contains("第一段正文", imported.Content);
            Assert.Contains(imported.Warnings, w => w.Contains("图片"));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public async Task ImportAsync_UnsupportedExtension_Throws()
    {
        var path = CreateTempFile("数据.xyz", Encoding.UTF8.GetBytes("hello"));

        try
        {
            var service = new DocumentImportService();
            Assert.False(service.CanImport(path));

            var exception = await Assert.ThrowsAsync<DocumentImportException>(() => service.ImportAsync(path));
            Assert.False(string.IsNullOrWhiteSpace(exception.UserMessage));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public async Task ImportAsync_MissingFile_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.md");

        var exception = await Assert.ThrowsAsync<DocumentImportException>(
            () => new DocumentImportService().ImportAsync(path));

        Assert.Contains("文件", exception.UserMessage);
    }

    [Fact]
    public async Task ImportAsync_InvalidLegacyDoc_FailsWithActionableMessage()
    {
        var bytes = new byte[4096];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)(i % 8);
        }

        var path = CreateTempFile("旧文档.doc", bytes);

        try
        {
            var exception = await Assert.ThrowsAsync<DocumentImportException>(
                () => new DocumentImportService().ImportAsync(path));

            Assert.Contains("docx", exception.UserMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
