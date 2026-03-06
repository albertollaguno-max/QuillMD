using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

// Aliases to resolve ambiguity between WPF and Markdig types
using WpfBlock = System.Windows.Documents.Block;
using WpfInline = System.Windows.Documents.Inline;
using MdBlock = Markdig.Syntax.Block;
using MdInline = Markdig.Syntax.Inlines.Inline;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableRow = Markdig.Extensions.Tables.TableRow;
using MdTableCell = Markdig.Extensions.Tables.TableCell;

namespace QuillMD.Services
{
    public static class MarkdownConverter
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        public static FlowDocument ToFlowDocument(string markdown, bool isDark)
        {
            var doc = new FlowDocument
            {
                PagePadding = new Thickness(40, 40, 40, 40),
                FontFamily = new FontFamily("Segoe UI, Calibri, Arial"),
                FontSize = 15,
                LineHeight = 26,
                TextAlignment = TextAlignment.Left,
                Background = isDark
                    ? new SolidColorBrush(Color.FromRgb(30, 30, 30))
                    : new SolidColorBrush(Color.FromRgb(250, 250, 250)),
                Foreground = isDark
                    ? new SolidColorBrush(Color.FromRgb(212, 212, 212))
                    : new SolidColorBrush(Color.FromRgb(36, 36, 36))
            };

            var parsed = Markdown.Parse(markdown, Pipeline);
            var headings = parsed.OfType<HeadingBlock>().ToList();

            foreach (var block in parsed)
            {
                WpfBlock? element;
                if (block is ParagraphBlock p && p.Inline?.FirstChild is LiteralInline lit
                    && lit.Content.ToString().Trim() == "[TOC]"
                    && lit.NextSibling == null)
                {
                    element = BuildTOC(headings, isDark);
                }
                else
                {
                    element = ConvertBlock(block, isDark);
                }
                if (element != null)
                    doc.Blocks.Add(element);
            }

            return doc;
        }

        private static WpfBlock? ConvertBlock(MdBlock block, bool isDark)
        {
            return block switch
            {
                HeadingBlock h => ConvertHeading(h, isDark),
                // Specific extensions must come before their base types
                Markdig.Extensions.Mathematics.MathBlock mb => ConvertMathBlock(mb, isDark),
                Markdig.Extensions.Yaml.YamlFrontMatterBlock yaml => ConvertYamlFrontMatter(yaml, isDark),
                FencedCodeBlock fcb => ConvertFencedCodeBlock(fcb, isDark),
                CodeBlock cb => ConvertCodeBlock(cb, isDark),
                QuoteBlock qb => ConvertBlockquote(qb, isDark),
                Markdig.Extensions.Footnotes.FootnoteGroup fg => ConvertFootnoteGroup(fg, isDark),
                ListBlock lb => ConvertList(lb, isDark),
                ThematicBreakBlock => ConvertHorizontalRule(isDark),
                Markdig.Extensions.Tables.Table t => ConvertTable(t, isDark),
                HtmlBlock htmlBlock => ConvertHtmlBlock(htmlBlock, isDark),
                ParagraphBlock p => ConvertParagraph(p, isDark),
                _ => null
            };
        }

        private static Paragraph ConvertHeading(HeadingBlock h, bool isDark)
        {
            var paragraph = new Paragraph
            {
                Margin = new Thickness(0, h.Level == 1 ? 20 : 14, 0, 6)
            };

            var (fontSize, bottomBorder) = h.Level switch
            {
                1 => (28.0, 1.5),
                2 => (22.0, 1.0),
                3 => (18.0, 0.0),
                4 => (16.0, 0.0),
                5 => (14.0, 0.0),
                _ => (13.0, 0.0)
            };

            var headingColor = isDark
                ? Color.FromRgb(86, 156, 214)
                : Color.FromRgb(0, 100, 180);

            paragraph.FontSize = fontSize;
            paragraph.FontWeight = FontWeights.SemiBold;
            paragraph.Foreground = new SolidColorBrush(headingColor);

            if (h.Inline != null)
                foreach (var mdInline in h.Inline)
                    foreach (var wpfInline in ConvertInline(mdInline, isDark))
                        paragraph.Inlines.Add(wpfInline);

            if (bottomBorder > 0)
            {
                paragraph.BorderBrush = isDark
                    ? new SolidColorBrush(Color.FromRgb(60, 60, 60))
                    : new SolidColorBrush(Color.FromRgb(200, 200, 200));
                paragraph.BorderThickness = new Thickness(0, 0, 0, bottomBorder);
                paragraph.Padding = new Thickness(0, 0, 0, 8);
            }

            return paragraph;
        }

        private static Paragraph ConvertParagraph(ParagraphBlock p, bool isDark)
        {
            var paragraph = new Paragraph
            {
                Margin = new Thickness(0, 4, 0, 8)
            };
            if (p.Inline != null)
                foreach (var mdInline in p.Inline)
                    foreach (var wpfInline in ConvertInline(mdInline, isDark))
                        paragraph.Inlines.Add(wpfInline);
            return paragraph;
        }

        private static WpfBlock ConvertFencedCodeBlock(FencedCodeBlock cb, bool isDark)
            => BuildCodeBlock(cb.Lines.ToString(), isDark);

        private static WpfBlock ConvertCodeBlock(CodeBlock cb, bool isDark)
            => BuildCodeBlock(cb.Lines.ToString(), isDark);

        private static WpfBlock BuildCodeBlock(string rawCode, bool isDark)
        {
            var bg = isDark
                ? new SolidColorBrush(Color.FromRgb(24, 24, 28))
                : new SolidColorBrush(Color.FromRgb(242, 242, 242));
            var fg = isDark
                ? new SolidColorBrush(Color.FromRgb(206, 145, 120))
                : new SolidColorBrush(Color.FromRgb(150, 0, 0));
            var borderColor = isDark
                ? new SolidColorBrush(Color.FromRgb(75, 75, 80))
                : new SolidColorBrush(Color.FromRgb(195, 195, 195));

            // Trim trailing blank line that Markdig tends to add
            string code = rawCode.TrimEnd('\r', '\n', ' ');

            var section = new Section
            {
                Background = bg,
                BorderBrush = borderColor,
                BorderThickness = new Thickness(4, 0, 0, 0),   // Left accent bar
                Margin = new Thickness(0, 10, 0, 10),
                Padding = new Thickness(14, 8, 14, 8),
            };

            // Split into lines so each line is its own Paragraph (no word wrap issues)
            var lines = code.Split('\n');
            foreach (var rawLine in lines)
            {
                string ln = rawLine.TrimEnd('\r');
                var para = new Paragraph
                {
                    Margin = new Thickness(0),
                    Padding = new Thickness(0),
                    LineHeight = 20,
                    FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
                    FontSize = 13,
                    Foreground = fg,
                    Background = bg,
                };
                para.Inlines.Add(new Run(ln));
                section.Blocks.Add(para);
            }

            return section;
        }

        private static Section ConvertBlockquote(QuoteBlock qb, bool isDark)
        {
            var section = new Section
            {
                Margin = new Thickness(0, 8, 0, 8),
                Padding = new Thickness(16, 4, 4, 4),
                BorderBrush = isDark
                    ? new SolidColorBrush(Color.FromRgb(100, 155, 90))
                    : new SolidColorBrush(Color.FromRgb(0, 150, 0)),
                BorderThickness = new Thickness(4, 0, 0, 0),
                Foreground = isDark
                    ? new SolidColorBrush(Color.FromRgb(100, 155, 90))
                    : new SolidColorBrush(Color.FromRgb(80, 130, 80))
            };

            foreach (var child in qb)
            {
                var el = ConvertBlock(child, isDark);
                if (el != null) section.Blocks.Add(el);
            }

            return section;
        }

        private static System.Windows.Documents.List ConvertList(ListBlock lb, bool isDark)
        {
            // Detect task list: first item contains a TaskList inline
            bool isTaskList = lb.OfType<ListItemBlock>().Any(item =>
                item.OfType<ParagraphBlock>().Any(p =>
                    p.Inline?.Any(i => i is Markdig.Extensions.TaskLists.TaskList) == true));

            var list = new System.Windows.Documents.List
            {
                Margin = new Thickness(0, 4, 0, 8),
                Padding = new Thickness(24, 0, 0, 0),
                MarkerStyle = isTaskList ? TextMarkerStyle.None
                    : lb.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc
            };

            foreach (var item in lb.OfType<ListItemBlock>())
            {
                var listItem = new ListItem();
                foreach (var child in item)
                {
                    var el = ConvertBlock(child, isDark);
                    if (el != null) listItem.Blocks.Add(el);
                }
                list.ListItems.Add(listItem);
            }

            return list;
        }

        private static Paragraph ConvertHorizontalRule(bool isDark)
        {
            return new Paragraph
            {
                BorderBrush = isDark
                    ? new SolidColorBrush(Color.FromRgb(70, 70, 70))
                    : new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Margin = new Thickness(0, 12, 0, 12)
            };
        }

        private static WpfBlock ConvertTable(MdTable table, bool isDark)
        {
            var borderBrush = isDark
                ? new SolidColorBrush(Color.FromRgb(70, 70, 70))
                : new SolidColorBrush(Color.FromRgb(200, 200, 200));
            var headerBg = isDark
                ? new SolidColorBrush(Color.FromRgb(45, 45, 48))
                : new SolidColorBrush(Color.FromRgb(230, 230, 230));

            var wpfTable = new System.Windows.Documents.Table
            {
                CellSpacing = 0,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0.5),
                Margin = new Thickness(0, 8, 0, 8)
            };

            // Determine column count from first row
            int colCount = table.OfType<MdTableRow>().FirstOrDefault()?.Count ?? 1;
            for (int i = 0; i < colCount; i++)
                wpfTable.Columns.Add(new System.Windows.Documents.TableColumn());

            var rowGroup = new System.Windows.Documents.TableRowGroup();
            bool isHeader = true;

            foreach (var mdRow in table.OfType<MdTableRow>())
            {
                var row = new System.Windows.Documents.TableRow();
                if (isHeader)
                {
                    row.Background = headerBg;
                    row.FontWeight = FontWeights.SemiBold;
                }

                int colIdx = 0;
                foreach (var mdCell in mdRow.OfType<MdTableCell>())
                {
                    var cell = new System.Windows.Documents.TableCell
                    {
                        BorderBrush = borderBrush,
                        BorderThickness = new Thickness(0.5),
                        Padding = new Thickness(8, 4, 8, 4)
                    };

                    var para = new Paragraph { Margin = new Thickness(0) };

                    // Apply column alignment from Markdown syntax (:---:, ---:, etc.)
                    if (colIdx < table.ColumnDefinitions.Count)
                    {
                        var align = table.ColumnDefinitions[colIdx].Alignment;
                        if (align == TableColumnAlign.Center)
                            para.TextAlignment = TextAlignment.Center;
                        else if (align == TableColumnAlign.Right)
                            para.TextAlignment = TextAlignment.Right;
                    }

                    foreach (var childBlock in mdCell)
                    {
                        if (childBlock is ParagraphBlock pb && pb.Inline != null)
                            foreach (var mdInline in pb.Inline)
                                foreach (var wpfInline in ConvertInline(mdInline, isDark))
                                    para.Inlines.Add(wpfInline);
                    }
                    cell.Blocks.Add(para);
                    row.Cells.Add(cell);
                    colIdx++;
                }

                rowGroup.Rows.Add(row);
                isHeader = false;
            }

            wpfTable.RowGroups.Add(rowGroup);
            return wpfTable;
        }

        private static Paragraph ConvertHtmlBlock(HtmlBlock html, bool isDark)
        {
            return new Paragraph
            {
                Foreground = isDark
                    ? new SolidColorBrush(Color.FromRgb(128, 128, 128))
                    : new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Inlines = { new Run(html.Lines.ToString()) }
            };
        }

        private static IEnumerable<WpfInline> ConvertInline(MdInline inline, bool isDark)
        {
            switch (inline)
            {
                case LiteralInline lit:
                    yield return new Run(lit.Content.ToString());
                    break;

                case EmphasisInline em:
                    var span = new Span();
                    if (em.DelimiterChar == '~' && em.DelimiterCount == 2)
                    {
                        span.TextDecorations = TextDecorations.Strikethrough;
                    }
                    else if (em.DelimiterCount >= 2)
                    {
                        span.FontWeight = FontWeights.Bold;
                    }
                    else
                    {
                        span.FontStyle = FontStyles.Italic;
                    }
                    foreach (var child in em)
                        foreach (var wpfChild in ConvertInline(child, isDark))
                            span.Inlines.Add(wpfChild);
                    yield return span;
                    break;

                case CodeInline code:
                    if (code.Content.Contains('\n'))
                    {
                        // Multi-line code inside single backticks.
                        // We use InlineUIContainer + Border to get a solid background and padding.
                        var container = new InlineUIContainer();
                        var border = new Border
                        {
                            Background = isDark 
                                ? new SolidColorBrush(Color.FromRgb(35, 35, 38)) 
                                : new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                            BorderBrush = isDark
                                ? new SolidColorBrush(Color.FromRgb(60, 60, 60))
                                : new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(10, 5, 10, 5),
                            CornerRadius = new CornerRadius(3),
                            Margin = new Thickness(0, 4, 0, 4)
                        };
                        var tb = new TextBlock
                        {
                            Text = code.Content.Trim(),
                            FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
                            FontSize = 13,
                            Foreground = isDark 
                                ? new SolidColorBrush(Color.FromRgb(206, 145, 120)) 
                                : new SolidColorBrush(Color.FromRgb(175, 0, 0)),
                            TextWrapping = TextWrapping.NoWrap
                        };
                        border.Child = tb;
                        container.Child = border;
                        yield return container;
                    }
                    else
                    {
                        yield return new Run(code.Content)
                        {
                            FontFamily = new FontFamily("Cascadia Code, Consolas"),
                            FontSize = 13,
                            Foreground = isDark
                                ? new SolidColorBrush(Color.FromRgb(206, 145, 120))
                                : new SolidColorBrush(Color.FromRgb(175, 0, 0)),
                            Background = isDark
                                ? new SolidColorBrush(Color.FromRgb(45, 45, 48))
                                : new SolidColorBrush(Color.FromRgb(235, 235, 235))
                        };
                    }
                    break;

                case LinkInline link when link.IsImage:
                    var imgContainer = new InlineUIContainer();
                    try
                    {
                        string imgPath = link.Url ?? "";
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        if (Uri.TryCreate(imgPath, UriKind.Absolute, out var absUri))
                            bitmap.UriSource = absUri;
                        else
                            bitmap.UriSource = new Uri(imgPath, UriKind.RelativeOrAbsolute);
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.EndInit();

                        var image = new System.Windows.Controls.Image
                        {
                            Source = bitmap,
                            MaxWidth = 600,
                            MaxHeight = 400,
                            Stretch = System.Windows.Media.Stretch.Uniform,
                            Margin = new Thickness(0, 4, 0, 4)
                        };

                        string altText = "";
                        foreach (var child in link)
                            if (child is LiteralInline altLit)
                                altText += altLit.Content.ToString();
                        if (!string.IsNullOrEmpty(altText))
                            image.ToolTip = altText;

                        imgContainer.Child = image;
                    }
                    catch
                    {
                        imgContainer.Child = new System.Windows.Controls.TextBlock
                        {
                            Text = $"[Imagen no encontrada: {link.Url}]",
                            FontStyle = FontStyles.Italic,
                            Foreground = new SolidColorBrush(Colors.Gray)
                        };
                    }
                    yield return imgContainer;
                    break;

                case LinkInline link:
                    {
                        var hyperlink = new Hyperlink
                        {
                            NavigateUri = Uri.TryCreate(link.Url ?? "", UriKind.Absolute, out var uri) ? uri : null,
                            Foreground = isDark
                                ? new SolidColorBrush(Color.FromRgb(78, 201, 176))
                                : new SolidColorBrush(Color.FromRgb(0, 100, 180))
                        };
                        hyperlink.RequestNavigate += (s, e) =>
                        {
                            if (e.Uri != null)
                                System.Diagnostics.Process.Start(
                                    new System.Diagnostics.ProcessStartInfo(e.Uri.ToString())
                                    { UseShellExecute = true });
                        };
                        foreach (var child in link)
                            foreach (var wpfChild in ConvertInline(child, isDark))
                                hyperlink.Inlines.Add(wpfChild);
                        yield return hyperlink;
                    }
                    break;

                case LineBreakInline:
                    yield return new LineBreak();
                    break;

                case HtmlInline htmlInl:
                    if (htmlInl.Tag is "<br>" or "<br/>")
                        yield return new LineBreak();
                    break;

                case ContainerInline container:
                    foreach (var child in container)
                        foreach (var wpfChild in ConvertInline(child, isDark))
                            yield return wpfChild;
                    break;

                case Markdig.Extensions.TaskLists.TaskList taskItem:
                    var cb = new System.Windows.Controls.CheckBox
                    {
                        IsChecked = taskItem.Checked,
                        IsHitTestVisible = false,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 4, 0)
                    };
                    yield return new InlineUIContainer(cb) { BaselineAlignment = BaselineAlignment.Center };
                    break;

                case Markdig.Extensions.Footnotes.FootnoteLink fl:
                    yield return new Run($"[{fl.Index}]")
                    {
                        FontSize = 10,
                        BaselineAlignment = BaselineAlignment.Superscript,
                        Foreground = isDark
                            ? new SolidColorBrush(Color.FromRgb(86, 156, 214))
                            : new SolidColorBrush(Color.FromRgb(0, 100, 180))
                    };
                    break;

                case Markdig.Extensions.Mathematics.MathInline mi:
                    yield return new Run(mi.Content.ToString())
                    {
                        FontFamily = new FontFamily("Cambria Math, Consolas"),
                        FontStyle = FontStyles.Italic,
                        Foreground = isDark
                            ? new SolidColorBrush(Color.FromRgb(181, 206, 168))
                            : new SolidColorBrush(Color.FromRgb(0, 100, 50)),
                        Background = isDark
                            ? new SolidColorBrush(Color.FromRgb(30, 30, 35))
                            : new SolidColorBrush(Color.FromRgb(248, 248, 248))
                    };
                    break;

                default:
                    break;
            }
        }
        private static Section BuildTOC(List<HeadingBlock> headings, bool isDark)
        {
            var section = new Section
            {
                Margin = new Thickness(0, 8, 0, 16),
                Padding = new Thickness(16, 8, 16, 8),
                Background = isDark
                    ? new SolidColorBrush(Color.FromRgb(35, 35, 38))
                    : new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                BorderBrush = isDark
                    ? new SolidColorBrush(Color.FromRgb(60, 60, 60))
                    : new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };

            var title = new Paragraph(new Run("Tabla de Contenidos")
            {
                FontWeight = FontWeights.SemiBold
            }) { Margin = new Thickness(0, 0, 0, 8) };
            section.Blocks.Add(title);

            foreach (var h in headings)
            {
                string text = "";
                if (h.Inline != null)
                    foreach (var inline in h.Inline)
                        if (inline is LiteralInline l)
                            text += l.Content.ToString();
                int indent = (h.Level - 1) * 20;
                var para = new Paragraph
                {
                    Margin = new Thickness(indent, 2, 0, 2),
                    FontSize = 13
                };
                para.Inlines.Add(new Run(text)
                {
                    Foreground = isDark
                        ? new SolidColorBrush(Color.FromRgb(78, 201, 176))
                        : new SolidColorBrush(Color.FromRgb(0, 100, 180))
                });
                section.Blocks.Add(para);
            }

            return section;
        }

        private static Section ConvertFootnoteGroup(Markdig.Extensions.Footnotes.FootnoteGroup fg, bool isDark)
        {
            var section = new Section
            {
                Margin = new Thickness(0, 20, 0, 0),
                BorderBrush = isDark
                    ? new SolidColorBrush(Color.FromRgb(60, 60, 60))
                    : new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0, 8, 0, 0)
            };

            var title = new Paragraph(new Run("Notas al pie")
            {
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = isDark
                    ? new SolidColorBrush(Color.FromRgb(128, 128, 128))
                    : new SolidColorBrush(Color.FromRgb(100, 100, 100))
            }) { Margin = new Thickness(0, 0, 0, 4) };
            section.Blocks.Add(title);

            foreach (var footnote in fg.OfType<Markdig.Extensions.Footnotes.Footnote>())
            {
                foreach (var child in footnote)
                {
                    var el = ConvertBlock(child, isDark);
                    if (el is Paragraph p)
                    {
                        var label = new Run($"[{footnote.Order}] ")
                        {
                            FontWeight = FontWeights.Bold,
                            FontSize = 12,
                            Foreground = isDark
                                ? new SolidColorBrush(Color.FromRgb(86, 156, 214))
                                : new SolidColorBrush(Color.FromRgb(0, 100, 180))
                        };
                        p.Inlines.InsertBefore(p.Inlines.FirstInline, label);
                        p.FontSize = 13;
                        section.Blocks.Add(p);
                    }
                    else if (el != null)
                    {
                        section.Blocks.Add(el);
                    }
                }
            }

            return section;
        }

        private static WpfBlock ConvertMathBlock(Markdig.Extensions.Mathematics.MathBlock mb, bool isDark)
        {
            string latex = mb.Lines.ToString().Trim();
            var bg = isDark
                ? new SolidColorBrush(Color.FromRgb(30, 30, 35))
                : new SolidColorBrush(Color.FromRgb(248, 248, 248));
            var fg = isDark
                ? new SolidColorBrush(Color.FromRgb(181, 206, 168))
                : new SolidColorBrush(Color.FromRgb(0, 100, 50));

            var para = new Paragraph
            {
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 12, 0, 12),
                Background = bg,
                Padding = new Thickness(16, 8, 16, 8),
                FontFamily = new FontFamily("Cambria Math, Consolas"),
                FontSize = 16,
                Foreground = fg,
                FontStyle = FontStyles.Italic
            };
            para.Inlines.Add(new Run(latex));
            return para;
        }

        private static WpfBlock ConvertYamlFrontMatter(Markdig.Extensions.Yaml.YamlFrontMatterBlock yaml, bool isDark)
        {
            string content = yaml.Lines.ToString().Trim();
            var bg = isDark
                ? new SolidColorBrush(Color.FromRgb(35, 35, 40))
                : new SolidColorBrush(Color.FromRgb(245, 245, 250));
            var fg = isDark
                ? new SolidColorBrush(Color.FromRgb(156, 170, 190))
                : new SolidColorBrush(Color.FromRgb(80, 80, 120));

            var section = new Section
            {
                Background = bg,
                BorderBrush = isDark
                    ? new SolidColorBrush(Color.FromRgb(60, 60, 80))
                    : new SolidColorBrush(Color.FromRgb(180, 180, 210)),
                BorderThickness = new Thickness(0, 0, 0, 2),
                Margin = new Thickness(0, 0, 0, 16),
                Padding = new Thickness(14, 8, 14, 8)
            };

            var header = new Paragraph(new Run("YAML Front Matter")
            {
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Foreground = isDark
                    ? new SolidColorBrush(Color.FromRgb(100, 100, 130))
                    : new SolidColorBrush(Color.FromRgb(120, 120, 150))
            }) { Margin = new Thickness(0, 0, 0, 4) };
            section.Blocks.Add(header);

            foreach (var line in content.Split('\n'))
            {
                var para = new Paragraph(new Run(line.TrimEnd('\r')))
                {
                    FontFamily = new FontFamily("Cascadia Code, Consolas"),
                    FontSize = 12,
                    Foreground = fg,
                    Margin = new Thickness(0),
                    LineHeight = 18
                };
                section.Blocks.Add(para);
            }

            return section;
        }
    }
}
