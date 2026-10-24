using System.Globalization;
using System.Text.RegularExpressions;

namespace SOrtus.Core.Markdown;

/// <summary>
/// Markdown 纯函数解析器（规范：详情页 V2 文档渲染）。
/// 只做块级拆分与待办勾选回写，不涉及任何 UI / IO；行内标记原样保留。
/// </summary>
public static class MarkdownParser
{
    /// <summary>待办项：缩进 + 列表符 + 空格 + [ ] / [x] / [X] + 文本。</summary>
    private static readonly Regex TaskRegex = new(
        @"^(?<indent>[ \t]*)(?<bullet>[-*+])[ \t]+\[(?<state>[ xX])\](?<rest>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>无序列表项：缩进 + - / * / + + 空格 + 文本。</summary>
    private static readonly Regex UnorderedListRegex = new(
        @"^(?<indent>[ \t]*)(?<bullet>[-*+])[ \t]+(?<text>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>有序列表项：缩进 + 数字 + '.' + 空格 + 文本。</summary>
    private static readonly Regex OrderedListRegex = new(
        @"^(?<indent>[ \t]*)(?<number>\d+)\.[ \t]+(?<text>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>表格分隔行中的单个单元格：<c>---</c> / <c>:--:</c> 之类。</summary>
    private static readonly Regex TableSeparatorCellRegex = new(
        @"^:?-{1,}:?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 解析 Markdown 文本为块列表。<paramref name="markdown"/> 为 null / 空串时返回
    /// <see cref="MarkdownDocument.Empty"/>，不抛异常。
    /// </summary>
    public static MarkdownDocument Parse(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return MarkdownDocument.Empty;
        }

        var lines = markdown.Split('\n');
        var blocks = new List<MarkdownBlock>();
        var index = 0;

        while (index < lines.Length)
        {
            var line = StripCarriageReturn(lines[index]);

            // 空行只结束当前块
            if (string.IsNullOrWhiteSpace(line))
            {
                index++;
                continue;
            }

            // 围栏代码块最先判定：块内 '#' 不得被误判为标题
            if (IsFenceLine(line))
            {
                var language = ParseFenceLanguage(line);
                var code = new List<string>();
                index++;
                while (index < lines.Length)
                {
                    var inner = StripCarriageReturn(lines[index]);
                    if (IsFenceLine(inner))
                    {
                        index++;
                        break;
                    }

                    code.Add(inner);
                    index++;
                }

                blocks.Add(new MarkdownCodeBlock(language, string.Join("\n", code)));
                continue;
            }

            // ATX 标题
            if (TryParseHeading(line, out var heading))
            {
                blocks.Add(heading);
                index++;
                continue;
            }

            // 分隔线必须在列表判定之前
            if (IsDivider(line))
            {
                blocks.Add(new MarkdownDivider());
                index++;
                continue;
            }

            // 引用块：连续行合并
            if (IsQuoteLine(line))
            {
                var quoted = new List<string>();
                while (index < lines.Length)
                {
                    var current = StripCarriageReturn(lines[index]);
                    if (!IsQuoteLine(current))
                    {
                        break;
                    }

                    quoted.Add(StripQuoteMarker(current));
                    index++;
                }

                blocks.Add(new MarkdownQuote(string.Join(" ", quoted).Trim()));
                continue;
            }

            // 表格：首行以 '|' 开头且次行为分隔行
            if (IsTableRowLine(line) &&
                index + 1 < lines.Length &&
                IsTableSeparator(StripCarriageReturn(lines[index + 1])))
            {
                var header = SplitCells(line);
                var rows = new List<IReadOnlyList<string>>();
                index += 2;
                while (index < lines.Length)
                {
                    var current = StripCarriageReturn(lines[index]);
                    if (!IsTableRowLine(current))
                    {
                        break;
                    }

                    rows.Add(SplitCells(current));
                    index++;
                }

                blocks.Add(new MarkdownTable(header, rows));
                continue;
            }

            // 待办项必须在普通列表判定之前
            var taskMatch = TaskRegex.Match(line);
            if (taskMatch.Success)
            {
                var state = taskMatch.Groups["state"].Value;
                var isChecked = state is "x" or "X";
                blocks.Add(new MarkdownTaskItem(
                    IndentLevel(taskMatch.Groups["indent"].Value),
                    taskMatch.Groups["rest"].Value.Trim(),
                    isChecked,
                    index));
                index++;
                continue;
            }

            // 无序列表
            var unorderedMatch = UnorderedListRegex.Match(line);
            if (unorderedMatch.Success)
            {
                blocks.Add(new MarkdownListItem(
                    IndentLevel(unorderedMatch.Groups["indent"].Value),
                    false,
                    0,
                    unorderedMatch.Groups["text"].Value.Trim()));
                index++;
                continue;
            }

            // 有序列表
            var orderedMatch = OrderedListRegex.Match(line);
            if (orderedMatch.Success)
            {
                var number = int.TryParse(
                    orderedMatch.Groups["number"].Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsed)
                    ? parsed
                    : 0;
                blocks.Add(new MarkdownListItem(
                    IndentLevel(orderedMatch.Groups["indent"].Value),
                    true,
                    number,
                    orderedMatch.Groups["text"].Value.Trim()));
                index++;
                continue;
            }

            // 段落：连续非空行合并，直到遇到空行或新的块起始行
            var paragraph = new List<string>();
            while (index < lines.Length)
            {
                var current = StripCarriageReturn(lines[index]);
                if (string.IsNullOrWhiteSpace(current))
                {
                    break;
                }

                if (paragraph.Count > 0 && StartsNewBlock(lines, index))
                {
                    break;
                }

                paragraph.Add(current.Trim());
                index++;
            }

            blocks.Add(new MarkdownParagraph(string.Join(" ", paragraph)));
        }

        return new MarkdownDocument { Blocks = blocks };
    }

    /// <summary>
    /// 勾选 / 取消勾选指定行（0 基行号，按 <c>\n</c> 切分）的待办项。
    /// 只替换 <c>[ ]</c> 与 <c>[x]</c> 之间的状态字符，缩进、列表符、后续文本与原始行尾
    /// （<c>\r\n</c> 文档仍输出 <c>\r\n</c>）全部保留。
    /// 行号越界或该行不是待办项时原样返回，不抛异常。
    /// </summary>
    public static string ToggleTask(string markdown, int sourceLine, bool isChecked)
    {
        if (markdown is null)
        {
            return string.Empty;
        }

        var lines = markdown.Split('\n');
        if (sourceLine < 0 || sourceLine >= lines.Length)
        {
            return markdown;
        }

        var line = lines[sourceLine];
        var hasCarriageReturn = line.EndsWith('\r');
        var body = StripCarriageReturn(line);

        var match = TaskRegex.Match(body);
        if (!match.Success)
        {
            return markdown;
        }

        var stateGroup = match.Groups["state"];
        var replacement = isChecked ? "x" : " ";
        var newBody = body[..stateGroup.Index] + replacement + body[(stateGroup.Index + stateGroup.Length)..];

        lines[sourceLine] = hasCarriageReturn ? newBody + "\r" : newBody;
        return string.Join("\n", lines);
    }

    /// <summary>
    /// 读取指定行的待办状态。命中返回 true 并输出状态；行号越界或该行不是待办项时返回 false。
    /// </summary>
    public static bool TryGetTaskState(string markdown, int sourceLine, out bool isChecked)
    {
        isChecked = false;
        if (markdown is null)
        {
            return false;
        }

        var lines = markdown.Split('\n');
        if (sourceLine < 0 || sourceLine >= lines.Length)
        {
            return false;
        }

        var match = TaskRegex.Match(StripCarriageReturn(lines[sourceLine]));
        if (!match.Success)
        {
            return false;
        }

        var state = match.Groups["state"].Value;
        isChecked = state is "x" or "X";
        return true;
    }

    /// <summary>去掉行尾的 <c>\r</c>（兼容 CRLF 文档）。</summary>
    private static string StripCarriageReturn(string line) =>
        line.EndsWith('\r') ? line[..^1] : line;

    /// <summary>判断该行是否为围栏标记行（三个反引号开头）。</summary>
    private static bool IsFenceLine(string line) =>
        line.TrimStart().StartsWith("```", StringComparison.Ordinal);

    /// <summary>从围栏标记行提取语言标识；无语言时返回 null。</summary>
    private static string? ParseFenceLanguage(string line)
    {
        var info = line.TrimStart()[3..].Trim();
        return info.Length == 0 ? null : info;
    }

    /// <summary>解析 ATX 标题（<c>#{1,6} 空格 文本</c>，去掉尾部多余 <c>#</c>）。</summary>
    private static bool TryParseHeading(string line, out MarkdownHeading heading)
    {
        heading = null!;
        var trimmed = line.TrimStart();

        var level = 0;
        while (level < trimmed.Length && trimmed[level] == '#')
        {
            level++;
        }

        if (level == 0 || level > 6)
        {
            return false;
        }

        // 标题标记后必须是空格（或整行仅有标题标记）
        if (level < trimmed.Length && trimmed[level] != ' ')
        {
            return false;
        }

        var text = level < trimmed.Length ? trimmed[(level + 1)..] : string.Empty;
        text = text.TrimEnd().TrimEnd('#').TrimEnd();

        heading = new MarkdownHeading(level, text);
        return true;
    }

    /// <summary>分隔线：整行只有同一种标记字符（<c>-</c> / <c>*</c> / <c>_</c>）与空白，且不少于 3 个。</summary>
    private static bool IsDivider(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length < 3)
        {
            return false;
        }

        var compact = trimmed.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\t", string.Empty, StringComparison.Ordinal);
        if (compact.Length < 3)
        {
            return false;
        }

        var marker = compact[0];
        if (marker is not ('-' or '*' or '_'))
        {
            return false;
        }

        foreach (var character in compact)
        {
            if (character != marker)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>引用行：以 <c>&gt;</c> 开头（允许前置缩进）。</summary>
    private static bool IsQuoteLine(string line) => line.TrimStart().StartsWith('>');

    /// <summary>去掉引用标记及其后一个空格。</summary>
    private static string StripQuoteMarker(string line)
    {
        var trimmed = line.TrimStart();
        var rest = trimmed.Length > 1 ? trimmed[1..] : string.Empty;
        return rest.StartsWith(' ') ? rest[1..] : rest;
    }

    /// <summary>表格行：以 <c>|</c> 开头（允许前置缩进）。</summary>
    private static bool IsTableRowLine(string line) => line.TrimStart().StartsWith('|');

    /// <summary>表格分隔行：每个单元格均为 <c>---</c> / <c>:--:</c> 形式。</summary>
    private static bool IsTableSeparator(string line)
    {
        if (!IsTableRowLine(line))
        {
            return false;
        }

        var cells = SplitCells(line);
        if (cells.Count == 0)
        {
            return false;
        }

        foreach (var cell in cells)
        {
            if (!TableSeparatorCellRegex.IsMatch(cell))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>拆分表格行为单元格（去首尾竖线、逐格 trim）。</summary>
    private static List<string> SplitCells(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith('|'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.EndsWith('|'))
        {
            trimmed = trimmed[..^1];
        }

        if (trimmed.Length == 0)
        {
            return new List<string>();
        }

        var cells = new List<string>();
        foreach (var cell in trimmed.Split('|'))
        {
            cells.Add(cell.Trim());
        }

        return cells;
    }

    /// <summary>缩进字符数换算为级别（2 空格 = 1 级，制表符按 2 空格计）。</summary>
    private static int IndentLevel(string indent) =>
        indent.Replace("\t", "  ", StringComparison.Ordinal).Length / 2;

    /// <summary>判断该行是否会开启一个新块（用于终止段落合并）。</summary>
    private static bool StartsNewBlock(string[] lines, int index)
    {
        var line = StripCarriageReturn(lines[index]);
        if (string.IsNullOrWhiteSpace(line))
        {
            return true;
        }

        if (IsFenceLine(line) ||
            TryParseHeading(line, out _) ||
            IsDivider(line) ||
            IsQuoteLine(line) ||
            TaskRegex.IsMatch(line) ||
            UnorderedListRegex.IsMatch(line) ||
            OrderedListRegex.IsMatch(line))
        {
            return true;
        }

        return IsTableRowLine(line) &&
               index + 1 < lines.Length &&
               IsTableSeparator(StripCarriageReturn(lines[index + 1]));
    }
}
