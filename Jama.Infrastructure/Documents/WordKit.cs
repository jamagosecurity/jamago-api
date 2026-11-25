using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;
using ImageSize = SixLabors.ImageSharp.Size;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace Jama.Infrastructure.Documents;

/// <summary>
/// Building blocks for the Word (.docx) exports.
///
/// Two rules every block keeps, because Word is stricter than any other viewer:
/// <list type="bullet">
/// <item>Child elements go in the order Word's schema requires. Out of order, Word calls the file damaged; the export
/// tests run Microsoft's own validator over every file.</item>
/// <item>Fonts, sizes and borders are set on each run and cell, not only as document defaults — phone viewers and Mac
/// Quick Look ignore the defaults and a table's "no borders".</item>
/// </list>
/// </summary>
internal static class WordKit
{
    // A4 in twentieths of a point, with half-inch margins.
    public const int PageWidth = 11906;
    public const int PageHeight = 16838;
    public const int Margin = 720;
    public const int ContentWidth = PageWidth - 2 * Margin;

    // Font sizes in half-points.
    public const int BodySize = 18;   // 9 pt
    public const int SmallSize = 16;  // 8 pt

    private const string Latin = "Calibri";
    private const string Arabic = "Arial";

    /// <summary>How a paragraph sits. Defaults: no spacing, no alignment.</summary>
    public sealed record Para(
        JustificationValues? Align = null,
        ParagraphBorders? Borders = null,
        bool KeepNext = false,
        bool PageBreakBefore = false,
        bool RightToLeft = false,
        int Before = 0,
        int After = 0);

    public static void AddStyles(MainDocumentPart main)
    {
        var styles = main.AddNewPart<StyleDefinitionsPart>();
        styles.Styles = new Styles(new DocDefaults(
            new RunPropertiesDefault(new RunProperties(
                new RunFonts { Ascii = Latin, HighAnsi = Latin, ComplexScript = Arabic, EastAsia = Latin },
                new Color { Val = DocumentLook.Ink },
                new FontSize { Val = BodySize.ToString() },
                new FontSizeComplexScript { Val = BodySize.ToString() })),
            new ParagraphPropertiesDefault(new ParagraphProperties(
                new SpacingBetweenLines { After = "0", Before = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto }))));
        styles.Styles.Save();
    }

    /// <summary>A footer with <paramref name="left"/> (if any) on the left and "Page X of Y" on the right.
    /// Returns the relationship id for the section's FooterReference.</summary>
    public static string AddFooter(MainDocumentPart main, string? left = null)
    {
        var part = main.AddNewPart<FooterPart>();
        var page = Par(new Para(JustificationValues.Right),
            R("Page ", color: DocumentLook.Muted, size: 14), Field("PAGE"),
            R(" of ", color: DocumentLook.Muted, size: 14), Field("NUMPAGES"));

        part.Footer = left is null
            ? new Footer(page)
            : new Footer(Grid([ContentWidth - 2400, 2400],
                new TableRow(
                    Cell(ContentWidth - 2400, [P(null, R(left, color: DocumentLook.Muted, size: 14))], noBorders: true),
                    Cell(2400, [page], noBorders: true))),
                P(null));
        part.Footer.Save();
        return main.GetIdOfPart(part);
    }

    private static SimpleField Field(string code) =>
        new(new Run(new RunProperties(new Color { Val = DocumentLook.Muted }, new FontSize { Val = "14" }), new Text("1")))
        { Instruction = $" {code} " };

    public static SectionProperties Section(string footerId) =>
        new(new FooterReference { Type = HeaderFooterValues.Default, Id = footerId },
            new PageSize { Width = PageWidth, Height = PageHeight },
            new PageMargin { Top = Margin, Bottom = Margin, Left = Margin, Right = Margin, Header = 360, Footer = 360, Gutter = 0 });

    /// <summary>A run with its font, size and colour set on the run itself. Properties in schema order.</summary>
    public static Run R(
        string text, bool bold = false, bool italic = false, string? color = null, int? size = null,
        bool underline = false, bool rightToLeft = false)
    {
        var half = (size ?? BodySize).ToString();
        var props = new RunProperties(new RunFonts { Ascii = Latin, HighAnsi = Latin, ComplexScript = Arabic, EastAsia = Latin });
        if (bold)
        {
            props.Append(new Bold());
            props.Append(new BoldComplexScript());
        }
        if (italic) props.Append(new Italic());
        props.Append(new Color { Val = color ?? DocumentLook.Ink });
        props.Append(new FontSize { Val = half });
        props.Append(new FontSizeComplexScript { Val = half });
        if (underline) props.Append(new Underline { Val = UnderlineValues.Single });
        if (rightToLeft) props.Append(new RightToLeftText());
        return new Run(props, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    public static Paragraph P(JustificationValues? align, params OpenXmlElement[] runs) => Par(new Para(align), runs);

    /// <summary>A paragraph. Properties in the order Word's schema requires: keepNext, pageBreakBefore, borders,
    /// bidi, spacing, justification.</summary>
    public static Paragraph Par(Para style, params OpenXmlElement[] runs)
    {
        var props = new ParagraphProperties();
        if (style.KeepNext) props.Append(new KeepNext());
        if (style.PageBreakBefore) props.Append(new PageBreakBefore());
        if (style.Borders is not null) props.Append(style.Borders);
        if (style.RightToLeft) props.Append(new BiDi());
        props.Append(new SpacingBetweenLines
        {
            Before = style.Before.ToString(), After = style.After.ToString(), Line = "240", LineRule = LineSpacingRuleValues.Auto,
        });
        if (style.Align is { } a) props.Append(new Justification { Val = a });
        var p = new Paragraph(props);
        p.Append(runs);
        return p;
    }

    /// <summary>An empty paragraph exactly <paramref name="twips"/> tall.</summary>
    public static Paragraph Spacer(int twips) =>
        new(new ParagraphProperties(new SpacingBetweenLines { After = "0", Before = "0", Line = twips.ToString(), LineRule = LineSpacingRuleValues.Exact }));

    public static TableCell Cell(
        int width, OpenXmlElement[] content, string? fill = null, int span = 1, bool noBorders = false,
        string? borderColor = null, uint borderSize = 4, MergedCellValues? merge = null,
        TableVerticalAlignmentValues? valign = null)
    {
        // In the order Word's schema requires: width, span, merge, borders, shading, vertical alignment.
        var props = new TableCellProperties(new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa });
        if (span > 1) props.Append(new GridSpan { Val = span });
        if (merge is { } mergeValue) props.Append(new VerticalMerge { Val = mergeValue });
        if (noBorders)
            props.Append(new TableCellBorders(
                new TopBorder { Val = BorderValues.Nil }, new LeftBorder { Val = BorderValues.Nil },
                new BottomBorder { Val = BorderValues.Nil }, new RightBorder { Val = BorderValues.Nil }));
        else
        {
            var color = borderColor ?? DocumentLook.Border;
            props.Append(new TableCellBorders(
                new TopBorder { Val = BorderValues.Single, Size = borderSize, Color = color },
                new LeftBorder { Val = BorderValues.Single, Size = borderSize, Color = color },
                new BottomBorder { Val = BorderValues.Single, Size = borderSize, Color = color },
                new RightBorder { Val = BorderValues.Single, Size = borderSize, Color = color }));
        }
        if (fill is not null) props.Append(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = fill });
        props.Append(new TableCellVerticalAlignment { Val = valign ?? TableVerticalAlignmentValues.Center });

        var cell = new TableCell(props);
        cell.Append(content);
        return cell;
    }

    /// <summary>A fixed-layout table. No table-level borders: each cell carries its own (or none).</summary>
    public static Table Grid(int[] widths, params TableRow[] rows) => Grid(widths, JustificationValues.Left, rows);

    public static Table Grid(int[] widths, JustificationValues align, params TableRow[] rows)
    {
        var props = new TableProperties(
            new TableWidth { Width = widths.Sum().ToString(), Type = TableWidthUnitValues.Dxa },
            new TableJustification { Val = align == JustificationValues.Center ? TableRowAlignmentValues.Center
                : align == JustificationValues.Right ? TableRowAlignmentValues.Right : TableRowAlignmentValues.Left },
            new TableLayout { Type = TableLayoutValues.Fixed },
            new TableCellMarginDefault(
                new TopMargin { Width = "20", Type = TableWidthUnitValues.Dxa },
                new TableCellLeftMargin { Width = 80, Type = TableWidthValues.Dxa },
                new BottomMargin { Width = "20", Type = TableWidthUnitValues.Dxa },
                new TableCellRightMargin { Width = 80, Type = TableWidthValues.Dxa }));

        var table = new Table(props, new TableGrid(widths.Select(w => new GridColumn { Width = w.ToString() })));
        table.Append(rows);
        return table;
    }

    /// <summary>A row that never splits across pages; <paramref name="header"/> repeats it on every page.</summary>
    public static TableRow Row(bool header, params TableCell[] cells)
    {
        var props = new TableRowProperties(new CantSplit());
        if (header) props.Append(new TableHeader());
        var row = new TableRow(props);
        row.Append(cells);
        return row;
    }

    public static Paragraph PageBreak() => new(new Run(new Break { Type = BreakValues.Page }));

    /// <summary>Adds pictures to one document, each with its own id as Word requires.</summary>
    public sealed class Pictures(MainDocumentPart main)
    {
        private uint _next = 1;

        /// <summary>A PNG as it is, <paramref name="widthEmu"/> wide (914 400 EMU to the inch).</summary>
        public Drawing Png(byte[] png, long widthEmu, string name)
        {
            var (w, h) = DocumentLook.PngSize(png);
            return Add(png, ImagePartType.Png, widthEmu, widthEmu * h / Math.Max(1, w), name);
        }

        /// <summary>
        /// Any photo (JPEG, PNG, WebP…), made into a small JPEG that fits a <paramref name="maxEmu"/> square — Word
        /// cannot show every format, and full-size product photos would make the file huge. Null when the bytes are
        /// not a picture.
        /// </summary>
        public Drawing? Photo(byte[] bytes, long maxEmu, string name)
        {
            try
            {
                using var image = Image.Load(bytes);
                image.Mutate(x => x.Resize(new ResizeOptions { Size = new ImageSize(240, 240), Mode = ResizeMode.Max }));
                using var output = new MemoryStream();
                SixLabors.ImageSharp.ImageExtensions.SaveAsJpeg(image, output);
                var scale = (double)maxEmu / Math.Max(image.Width, image.Height);
                return Add(output.ToArray(), ImagePartType.Jpeg, (long)(image.Width * scale), (long)(image.Height * scale), name);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private Drawing Add(byte[] bytes, PartTypeInfo type, long cx, long cy, string name)
        {
            var part = main.AddImagePart(type);
            using (var data = new MemoryStream(bytes)) part.FeedData(data);
            var relId = main.GetIdOfPart(part);
            var id = _next++;

            return new Drawing(new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = id, Name = name },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(new A.GraphicData(
                    new PIC.Picture(
                        new PIC.NonVisualPictureProperties(
                            new PIC.NonVisualDrawingProperties { Id = id, Name = name },
                            new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(new A.Blip { Embed = relId }, new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(
                            new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }),
                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            {
                DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U,
            });
        }
    }
}
