namespace SOrtus.Core.Markdown;

/// <summary>
/// Markdown 块级元素基类（纯数据，不依赖任何 UI 框架）。
/// 解析层只做块级切分，行内标记（<c>**b**</c> / <c>*i*</c> / <c>`c`</c> / <c>[t](u)</c>）原样保留给渲染层处理。
/// </summary>
public abstract record MarkdownBlock;

/// <summary>标题块（ATX 风格，Level 1~6）。</summary>
public sealed record MarkdownHeading(int Level, string Text) : MarkdownBlock;

/// <summary>段落块（连续非空行合并，行间以空格连接）。</summary>
public sealed record MarkdownParagraph(string Text) : MarkdownBlock;

/// <summary>
/// 列表项块。<paramref name="Indent"/> 为缩进级别（2 空格 = 1 级，0 基）；
/// <paramref name="IsOrdered"/> 为 true 时 <paramref name="Number"/> 有效，无序列表固定为 0。
/// </summary>
public sealed record MarkdownListItem(int Indent, bool IsOrdered, int Number, string Text) : MarkdownBlock;

/// <summary>
/// 待办项块。<paramref name="SourceLine"/> 为原始文档中该行的行号（0 基，按 <c>\n</c> 切分），
/// 供勾选回写时精确定位。
/// </summary>
public sealed record MarkdownTaskItem(int Indent, string Text, bool IsChecked, int SourceLine) : MarkdownBlock;

/// <summary>围栏代码块（无语言标识时 <paramref name="Language"/> 为 null）。</summary>
public sealed record MarkdownCodeBlock(string? Language, string Code) : MarkdownBlock;

/// <summary>引用块（连续 <c>&gt; </c> 行合并为一段）。</summary>
public sealed record MarkdownQuote(string Text) : MarkdownBlock;

/// <summary>分隔线（<c>---</c> / <c>***</c> / <c>___</c>）。</summary>
public sealed record MarkdownDivider : MarkdownBlock;

/// <summary>表格块（首行为表头，其余为数据行；单元格已 trim 并去掉首尾竖线）。</summary>
public sealed record MarkdownTable(IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<string>> Rows)
    : MarkdownBlock;

/// <summary>
/// 解析结果文档。仅承载块列表，无状态、无副作用。
/// </summary>
public sealed class MarkdownDocument
{
    /// <summary>块列表（按文档顺序）。</summary>
    public IReadOnlyList<MarkdownBlock> Blocks { get; init; } = Array.Empty<MarkdownBlock>();

    /// <summary>空文档单例（解析 null / 空串时返回）。</summary>
    public static MarkdownDocument Empty { get; } = new();
}
