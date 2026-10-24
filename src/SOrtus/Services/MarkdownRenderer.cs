using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SOrtus.Core.Markdown;

namespace SOrtus.Services;

/// <summary>
/// Markdown → FlowDocument 渲染契约（表现层，依赖 WPF 排版对象）。
/// <para>
/// 解析与渲染分层的边界是 <see cref="MarkdownDocument"/>：解析在 <c>SOrtus.Core</c> 里做纯函数（可单测），
/// 渲染在这里生成 WPF 文档。待办勾选通过 <c>onTodoToggled(sourceLine, isChecked)</c> 回调交给视图模型落库。
/// </para>
/// </summary>
public interface IMarkdownRenderer
{
    /// <summary>渲染文档；待办勾选时回调（行号为 Markdown 原文的 0 基行号）。</summary>
    FlowDocument Render(MarkdownDocument document, Action<int, bool>? onTodoToggled);

    /// <summary>按行号同步某个待办的勾选态（不触发回调），用于写库失败回滚。</summary>
    bool TrySetTaskState(FlowDocument document, int sourceLine, bool isChecked);
}

/// <summary>
/// 轻量 Markdown 渲染器（规范 7.1：只用主题令牌，不硬编码颜色）。
/// <para>
/// 一个实例只服务一个文档（内部持有「行号 → CheckBox」映射）；换文档请重新构造。
/// </para>
/// </summary>
internal sealed class MarkdownRenderer : IMarkdownRenderer
{
    private const string MonoFont = "Cascadia Mono, Consolas, Courier New";

    private readonly Dictionary<int, CheckBox> _taskBoxes = new();
    private bool _suppressToggle;
    private Action<int, bool>? _onTodoToggled;

    public FlowDocument Render(MarkdownDocument document, Action<int, bool>? onTodoToggled)
    {
        _taskBoxes.Clear();
        _onTodoToggled = onTodoToggled;

        var flow = new FlowDocument
        {
            PagePadding = new Thickness(0),
            ColumnWidth = double.PositiveInfinity,
            FontSize = 13,
            FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
        };

        foreach (var block in document.Blocks)
        {
            switch (block)
            {
                case MarkdownHeading heading:
                    flow.Blocks.Add(BuildHeading(heading));
                    break;
                case MarkdownParagraph paragraph:
                    flow.Blocks.Add(BuildParagraph(paragraph));
                    break;
                case MarkdownListItem item:
                    flow.Blocks.Add(BuildListItem(item));
                    break;
                case MarkdownTaskItem task:
                    flow.Blocks.Add(BuildTaskItem(task));
                    break;
                case MarkdownCodeBlock code:
                    flow.Blocks.Add(BuildCodeBlock(code));
                    break;
                case MarkdownQuote quote:
                    flow.Blocks.Add(BuildQuote(quote));
                    break;
                case MarkdownDivider:
                    flow.Blocks.Add(BuildDivider());
                    break;
                case MarkdownTable table:
                    flow.Blocks.Add(BuildTable(table));
                    break;
            }
        }

        if (flow.Blocks.Count == 0)
        {
            var empty = new Paragraph(new Run("（文档内容为空）"))
            {
                Foreground = Resource("TextSecondaryBrush", Brushes.Gray),
                FontStyle = FontStyles.Italic,
            };
            flow.Blocks.Add(empty);
        }

        return flow;
    }

    public bool TrySetTaskState(FlowDocument document, int sourceLine, bool isChecked)
    {
        if (document is null || !_taskBoxes.TryGetValue(sourceLine, out var box))
        {
            return false;
        }

        _suppressToggle = true;
        try
        {
            box.IsChecked = isChecked;
        }
        finally
        {
            _suppressToggle = false;
        }

        return true;
    }

    private static Paragraph BuildHeading(MarkdownHeading heading)
    {
        var size = heading.Level switch
        {
            1 => 26d,
            2 => 22d,
            3 => 18d,
            4 => 16d,
            5 => 14d,
            _ => 13d,
        };

        var top = heading.Level switch
        {
            1 => 18d,
            2 => 16d,
            3 => 14d,
            4 => 12d,
            _ => 10d,
        };

        var paragraph = new Paragraph
        {
            FontSize = size,
            FontWeight = heading.Level <= 3 ? FontWeights.Bold : FontWeights.SemiBold,
            Margin = new Thickness(0, top, 0, 8),
            LineHeight = size * 1.4,
            Foreground = Resource("TextPrimaryBrush", Brushes.Black),
        };
        AddInlines(paragraph, heading.Text);
        return paragraph;
    }

    private static Paragraph BuildParagraph(MarkdownParagraph source)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 9),
            LineHeight = 21,
            Foreground = Resource("TextPrimaryBrush", Brushes.Black),
        };
        AddInlines(paragraph, source.Text);
        return paragraph;
    }

    private static Paragraph BuildListItem(MarkdownListItem item)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(6 + (item.Indent * 16), 0, 0, 4),
            LineHeight = 20,
            Foreground = Resource("TextPrimaryBrush", Brushes.Black),
        };

        paragraph.Inlines.Add(new Run(item.IsOrdered ? $"{item.Number}. " : "• ")
        {
            Foreground = Resource("TextSecondaryBrush", Brushes.Gray),
        });
        AddInlines(paragraph, item.Text);
        return paragraph;
    }

    private Paragraph BuildTaskItem(MarkdownTaskItem task)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(6 + (task.Indent * 16), 0, 0, 4),
            LineHeight = 20,
            Foreground = Resource("TextPrimaryBrush", Brushes.Black),
        };

        var box = new CheckBox
        {
            IsChecked = task.IsChecked,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0),
            Focusable = true,
        };

        box.Checked += (_, _) => RaiseToggle(task.SourceLine, true);
        box.Unchecked += (_, _) => RaiseToggle(task.SourceLine, false);

        paragraph.Inlines.Add(new InlineUIContainer(box) { BaselineAlignment = BaselineAlignment.Center });
        AddInlines(paragraph, task.Text);

        if (task.IsChecked)
        {
            paragraph.Foreground = Resource("TextSecondaryBrush", Brushes.Gray);
        }

        _taskBoxes[task.SourceLine] = box;
        return paragraph;
    }

    private void RaiseToggle(int sourceLine, bool isChecked)
    {
        if (_suppressToggle)
        {
            return;
        }

        _onTodoToggled?.Invoke(sourceLine, isChecked);
    }

    private static Paragraph BuildCodeBlock(MarkdownCodeBlock code)
    {
        var paragraph = new Paragraph
        {
            FontFamily = new FontFamily(MonoFont),
            FontSize = 12,
            Background = Resource("SurfaceBrush", Brushes.WhiteSmoke),
            BorderBrush = Resource("BorderBrush", Brushes.LightGray),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 4, 0, 10),
            LineHeight = 18,
        };

        var lines = code.Code.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                paragraph.Inlines.Add(new LineBreak());
            }

            paragraph.Inlines.Add(new Run(lines[i]));
        }

        return paragraph;
    }

    private static Paragraph BuildQuote(MarkdownQuote quote)
    {
        var paragraph = new Paragraph
        {
            BorderBrush = Resource("PrimaryBrush", Brushes.SteelBlue),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(10, 2, 0, 2),
            Margin = new Thickness(0, 4, 0, 10),
            LineHeight = 21,
            FontStyle = FontStyles.Italic,
            Foreground = Resource("TextSecondaryBrush", Brushes.Gray),
        };
        AddInlines(paragraph, quote.Text);
        return paragraph;
    }

    private static Paragraph BuildDivider() => new(new Run(" "))
    {
        BorderBrush = Resource("BorderBrush", Brushes.LightGray),
        BorderThickness = new Thickness(0, 0, 0, 1),
        Margin = new Thickness(0, 10, 0, 12),
        FontSize = 2,
    };

    private static Table BuildTable(MarkdownTable source)
    {
        var border = Resource("BorderBrush", Brushes.LightGray);
        var table = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 4, 0, 12),
        };

        for (var i = 0; i < Math.Max(1, source.Header.Count); i++)
        {
            table.Columns.Add(new TableColumn());
        }

        var group = new TableRowGroup();
        table.RowGroups.Add(group);

        if (source.Header.Count > 0)
        {
            var headerRow = new TableRow { Background = Resource("SurfaceBrush", Brushes.WhiteSmoke) };
            foreach (var cell in source.Header)
            {
                headerRow.Cells.Add(BuildCell(cell, border, bold: true));
            }

            group.Rows.Add(headerRow);
        }

        foreach (var row in source.Rows)
        {
            var tableRow = new TableRow();
            foreach (var cell in row)
            {
                tableRow.Cells.Add(BuildCell(cell, border, bold: false));
            }

            group.Rows.Add(tableRow);
        }

        return table;
    }

    private static TableCell BuildCell(string text, Brush border, bool bold)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0),
            LineHeight = 19,
            Foreground = Resource("TextPrimaryBrush", Brushes.Black),
        };

        if (bold)
        {
            paragraph.FontWeight = FontWeights.SemiBold;
        }

        AddInlines(paragraph, text);

        return new TableCell(paragraph)
        {
            BorderBrush = border,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 5, 8, 5),
        };
    }

    /// <summary>
    /// 行内标记解析：<c>**粗体**</c>、<c>*斜体*</c>、<c>`代码`</c>、<c>[文本](链接)</c>。
    /// 未闭合的标记按普通文本处理，绝不吞字符。
    /// </summary>
    private static void AddInlines(Paragraph paragraph, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var index = 0;
        while (index < text.Length)
        {
            if (TryBold(text, ref index, paragraph)
                || TryCode(text, ref index, paragraph)
                || TryLink(text, ref index, paragraph)
                || TryItalic(text, ref index, paragraph))
            {
                continue;
            }

            var start = index;
            while (index < text.Length && text[index] is not '*' and not '`' and not '[')
            {
                index++;
            }

            if (index == start)
            {
                index++;
            }

            paragraph.Inlines.Add(new Run(text[start..index]));
        }
    }

    private static bool TryBold(string text, ref int index, Paragraph paragraph)
    {
        if (!text.AsSpan(index).StartsWith("**", StringComparison.Ordinal))
        {
            return false;
        }

        var close = text.IndexOf("**", index + 2, StringComparison.Ordinal);
        if (close <= index + 2)
        {
            return false;
        }

        paragraph.Inlines.Add(new Run(text[(index + 2)..close]) { FontWeight = FontWeights.Bold });
        index = close + 2;
        return true;
    }

    private static bool TryItalic(string text, ref int index, Paragraph paragraph)
    {
        if (text[index] != '*')
        {
            return false;
        }

        var close = text.IndexOf('*', index + 1);
        if (close <= index + 1)
        {
            return false;
        }

        paragraph.Inlines.Add(new Run(text[(index + 1)..close]) { FontStyle = FontStyles.Italic });
        index = close + 1;
        return true;
    }

    private static bool TryCode(string text, ref int index, Paragraph paragraph)
    {
        if (text[index] != '`')
        {
            return false;
        }

        var close = text.IndexOf('`', index + 1);
        if (close <= index + 1)
        {
            return false;
        }

        paragraph.Inlines.Add(new Run(text[(index + 1)..close])
        {
            FontFamily = new FontFamily(MonoFont),
            FontSize = 12,
            Background = Resource("SurfaceBrush", Brushes.WhiteSmoke),
        });
        index = close + 1;
        return true;
    }

    private static bool TryLink(string text, ref int index, Paragraph paragraph)
    {
        if (text[index] != '[')
        {
            return false;
        }

        var labelEnd = text.IndexOf(']', index + 1);
        if (labelEnd < 0 || labelEnd + 1 >= text.Length || text[labelEnd + 1] != '(')
        {
            return false;
        }

        var urlEnd = text.IndexOf(')', labelEnd + 2);
        if (urlEnd < 0)
        {
            return false;
        }

        var label = text[(index + 1)..labelEnd];
        var url = text[(labelEnd + 2)..urlEnd];
        var run = new Run(label.Length == 0 ? url : label)
        {
            Foreground = Resource("PrimaryBrush", Brushes.SteelBlue),
            TextDecorations = TextDecorations.Underline,
            ToolTip = url,
        };

        paragraph.Inlines.Add(run);
        index = urlEnd + 1;
        return true;
    }

    private static Brush Resource(string key, Brush fallback) =>
        Application.Current?.TryFindResource(key) as Brush ?? fallback;
}
