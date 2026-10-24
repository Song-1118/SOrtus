using SOrtus.Core.Markdown;
using Xunit;

namespace SOrtus.UnitTests;

/// <summary>
/// Markdown 纯函数解析器测试：覆盖块级元素识别、CRLF 兼容、待办勾选回写与边界容错。
/// 解析层不做行内标记处理，相关断言只校验原文保留。
/// </summary>
public class MarkdownParserTests
{
    [Theory]
    [InlineData("# 一级标题", 1)]
    [InlineData("## 二级标题", 2)]
    [InlineData("### 三级标题", 3)]
    [InlineData("#### 四级标题", 4)]
    [InlineData("##### 五级标题", 5)]
    [InlineData("###### 六级标题", 6)]
    public void Parse_Heading_DetectsLevel1To6(string markdown, int expectedLevel)
    {
        var document = MarkdownParser.Parse(markdown);

        var heading = Assert.IsType<MarkdownHeading>(Assert.Single(document.Blocks));
        Assert.Equal(expectedLevel, heading.Level);
        Assert.Equal("级标题", heading.Text[^3..]);
        Assert.EndsWith("级标题", heading.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Heading_StripsTrailingHashes()
    {
        var document = MarkdownParser.Parse("## 标题 ##");

        var heading = Assert.IsType<MarkdownHeading>(Assert.Single(document.Blocks));
        Assert.Equal(2, heading.Level);
        Assert.Equal("标题", heading.Text);
    }

    [Fact]
    public void Parse_Paragraph_MergesConsecutiveLines()
    {
        var document = MarkdownParser.Parse("第一行\n第二行");

        var paragraph = Assert.IsType<MarkdownParagraph>(Assert.Single(document.Blocks));
        Assert.Equal("第一行 第二行", paragraph.Text);
    }

    [Fact]
    public void Parse_Paragraph_PreservesInlineMarkupVerbatim()
    {
        var document = MarkdownParser.Parse("这是 **粗体** 与 `代码` 与 [链接](https://example.com)");

        var paragraph = Assert.IsType<MarkdownParagraph>(Assert.Single(document.Blocks));
        Assert.Equal("这是 **粗体** 与 `代码` 与 [链接](https://example.com)", paragraph.Text);
    }

    [Fact]
    public void Parse_UnorderedAndOrderedLists_SplitIntoItems()
    {
        var document = MarkdownParser.Parse("- 苹果\n- 香蕉\n\n1. 第一步\n2. 第二步");

        Assert.Equal(4, document.Blocks.Count);

        var first = Assert.IsType<MarkdownListItem>(document.Blocks[0]);
        Assert.False(first.IsOrdered);
        Assert.Equal(0, first.Indent);
        Assert.Equal("苹果", first.Text);

        var third = Assert.IsType<MarkdownListItem>(document.Blocks[2]);
        Assert.True(third.IsOrdered);
        Assert.Equal(1, third.Number);
        Assert.Equal("第一步", third.Text);

        var fourth = Assert.IsType<MarkdownListItem>(document.Blocks[3]);
        Assert.True(fourth.IsOrdered);
        Assert.Equal(2, fourth.Number);
        Assert.Equal("第二步", fourth.Text);
    }

    [Fact]
    public void Parse_NestedList_ComputesIndentLevel()
    {
        var document = MarkdownParser.Parse("- 顶层\n  - 子项\n    - 孙项");

        Assert.Equal(3, document.Blocks.Count);
        Assert.Equal(0, Assert.IsType<MarkdownListItem>(document.Blocks[0]).Indent);
        Assert.Equal(1, Assert.IsType<MarkdownListItem>(document.Blocks[1]).Indent);
        Assert.Equal(2, Assert.IsType<MarkdownListItem>(document.Blocks[2]).Indent);
    }

    [Theory]
    [InlineData("- [ ] 未完成", false)]
    [InlineData("- [x] 已完成", true)]
    [InlineData("* [x] 已完成", true)]
    [InlineData("+ [X] 已完成", true)]
    public void Parse_TaskItem_DetectsCheckedState(string markdown, bool expectedChecked)
    {
        var document = MarkdownParser.Parse(markdown);

        var task = Assert.IsType<MarkdownTaskItem>(Assert.Single(document.Blocks));
        Assert.Equal(expectedChecked, task.IsChecked);
        Assert.Equal(0, task.Indent);
        Assert.Equal(0, task.SourceLine);
    }

    [Fact]
    public void Parse_IndentedTask_TracksIndentAndSourceLine()
    {
        var document = MarkdownParser.Parse("说明\n\n  - [ ] 子任务");

        var task = Assert.Single(document.Blocks.OfType<MarkdownTaskItem>());
        Assert.Equal(1, task.Indent);
        Assert.Equal(2, task.SourceLine);
        Assert.Equal("子任务", task.Text);
    }

    [Fact]
    public void Parse_CodeFence_WithoutLanguage_KeepsHashAsCode()
    {
        var document = MarkdownParser.Parse("```\n# 不是标题\n```");

        var code = Assert.IsType<MarkdownCodeBlock>(Assert.Single(document.Blocks));
        Assert.Null(code.Language);
        Assert.Equal("# 不是标题", code.Code);
    }

    [Fact]
    public void Parse_CodeFence_WithLanguage_AndUnclosedRunsToEnd()
    {
        var document = MarkdownParser.Parse("```csharp\nvar x = 1;\n");

        var code = Assert.IsType<MarkdownCodeBlock>(Assert.Single(document.Blocks));
        Assert.Equal("csharp", code.Language);
        Assert.Equal("var x = 1;\n", code.Code);
    }

    [Fact]
    public void Parse_Quote_MergesConsecutiveLines()
    {
        var document = MarkdownParser.Parse("> 第一句\n> 第二句");

        var quote = Assert.IsType<MarkdownQuote>(Assert.Single(document.Blocks));
        Assert.Equal("第一句 第二句", quote.Text);
    }

    [Theory]
    [InlineData("---")]
    [InlineData("***")]
    [InlineData("___")]
    [InlineData("- - -")]
    public void Parse_Divider_IsRecognized(string markdown)
    {
        var document = MarkdownParser.Parse(markdown);

        Assert.IsType<MarkdownDivider>(Assert.Single(document.Blocks));
    }

    [Fact]
    public void Parse_Table_SplitsHeaderAndRows()
    {
        var markdown = "| 名称 | 说明 |\n| --- | :--: |\n| A | 第一 |\n| B | 第二 |";

        var document = MarkdownParser.Parse(markdown);

        var table = Assert.IsType<MarkdownTable>(Assert.Single(document.Blocks));
        Assert.Equal(new[] { "名称", "说明" }, table.Header.ToArray());
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(new[] { "A", "第一" }, table.Rows[0].ToArray());
        Assert.Equal(new[] { "B", "第二" }, table.Rows[1].ToArray());
    }

    [Fact]
    public void Parse_CrlfDocument_ParsesSameAsLf()
    {
        var markdown = "# 标题\r\n\r\n正文第一行\r\n正文第二行";

        var document = MarkdownParser.Parse(markdown);

        Assert.Equal(2, document.Blocks.Count);

        var heading = Assert.IsType<MarkdownHeading>(document.Blocks[0]);
        Assert.Equal(1, heading.Level);
        Assert.Equal("标题", heading.Text);

        var paragraph = Assert.IsType<MarkdownParagraph>(document.Blocks[1]);
        Assert.Equal("正文第一行 正文第二行", paragraph.Text);
    }

    [Fact]
    public void Parse_NullOrEmpty_ReturnsEmptyDocumentWithoutThrowing()
    {
        Assert.Empty(MarkdownParser.Parse(null).Blocks);
        Assert.Empty(MarkdownParser.Parse(string.Empty).Blocks);
        Assert.Empty(MarkdownDocument.Empty.Blocks);
    }

    [Fact]
    public void ToggleTask_ChecksThenUnchecks_TargetLineOnly()
    {
        const string markdown = "- [ ] 待办一\n- [ ] 待办二";

        var checkedMarkdown = MarkdownParser.ToggleTask(markdown, 1, true);

        Assert.Equal("- [ ] 待办一\n- [x] 待办二", checkedMarkdown);
        Assert.Equal(markdown, MarkdownParser.ToggleTask(checkedMarkdown, 1, false));
    }

    [Fact]
    public void ToggleTask_PreservesIndentAndCrlfLineEnding()
    {
        const string markdown = "# 标题\r\n  - [ ] 子任务\r\n";

        var toggled = MarkdownParser.ToggleTask(markdown, 1, true);

        Assert.Equal("# 标题\r\n  - [x] 子任务\r\n", toggled);
    }

    [Fact]
    public void ToggleTask_OutOfRangeOrNonTask_ReturnsOriginal()
    {
        const string markdown = "- [ ] 待办\n普通段落";

        Assert.Equal(markdown, MarkdownParser.ToggleTask(markdown, -1, true));
        Assert.Equal(markdown, MarkdownParser.ToggleTask(markdown, 99, true));
        Assert.Equal(markdown, MarkdownParser.ToggleTask(markdown, 1, true));
    }

    [Fact]
    public void TryGetTaskState_ReportsStateAndMisses()
    {
        const string markdown = "- [x] 已完成\n段落";

        Assert.True(MarkdownParser.TryGetTaskState(markdown, 0, out var isChecked));
        Assert.True(isChecked);

        Assert.True(MarkdownParser.TryGetTaskState("- [ ] 未完成", 0, out var uncheckedState));
        Assert.False(uncheckedState);

        Assert.False(MarkdownParser.TryGetTaskState(markdown, 1, out _));
        Assert.False(MarkdownParser.TryGetTaskState(markdown, 5, out _));
    }
}
