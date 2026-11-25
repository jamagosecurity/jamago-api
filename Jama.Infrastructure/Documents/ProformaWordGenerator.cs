using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Proformas;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace Jama.Infrastructure.Documents;

/// <summary>
/// The proforma invoice as a Word document, laid out like the PDF: logo and boxed title, the customer box beside the
/// references, one table grouped by section (headings repeat on each page), totals, notes, terms, payment details and
/// the company at the end. A4, page numbers in the footer. Everything stays editable.
/// </summary>
public sealed class ProformaWordGenerator : IProformaWordGenerator
{
    // A4 in twentieths of a point, with half-inch margins.
    private const int PageWidth = 11906;
    private const int PageHeight = 16838;
    private const int Margin = 720;
    private const int ContentWidth = PageWidth - 2 * Margin;

    private const int Body = 18;   // font sizes in half-points: 9 pt
    private const int Small = 16;  // 8 pt

    public byte[] Generate(ProformaModel m)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            doc.PackageProperties.Title = m.Number is null ? $"Proforma invoice (draft) — {m.QuoteNumber}" : $"Proforma invoice {m.Number}";
            doc.PackageProperties.Creator = "Jama Go Security Equipment";

            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body());
            AddStyles(main);
            var footerId = AddFooter(main);
            var body = main.Document.Body!;

            body.Append(Header(main, m));
            if (m.Draft) body.Append(P(JustificationValues.Center, R("DRAFT — NOT APPROVED", bold: true, color: ProformaLook.DraftRed, size: 22)));
            body.Append(Spacer(80));
            body.Append(Boxes(m));
            body.Append(Spacer(80));
            body.Append(Items(m));
            body.Append(Spacer(60));
            body.Append(Totals(m));
            body.Append(Spacer(160));
            body.Append(TermsAndPayment());
            body.Append(Spacer(240));
            foreach (var p in Company()) body.Append(p);

            body.Append(new SectionProperties(
                new FooterReference { Type = HeaderFooterValues.Default, Id = footerId },
                new PageSize { Width = PageWidth, Height = PageHeight },
                new PageMargin { Top = Margin, Bottom = Margin, Left = Margin, Right = Margin, Header = 360, Footer = 360, Gutter = 0 }));
            main.Document.Save();
        }
        return stream.ToArray();
    }

    // ---------- parts ----------

    private static Table Header(MainDocumentPart main, ProformaModel m)
    {
        var logo = ProformaLook.Logo.Length > 0
            ? P(JustificationValues.Left, new Run(Logo(main)))
            : P(JustificationValues.Left, R("JAMA GO", bold: true, color: ProformaLook.Brand, size: 40));

        var title = Cell(4200, [P(JustificationValues.Center, R("PROFORMA INVOICE", bold: true, color: ProformaLook.BrandStrong, size: 30))],
            borderColor: ProformaLook.BrandStrong, borderSize: 12);

        return Grid([ContentWidth - 4200, 4200], borders: false,
            new TableRow(Cell(ContentWidth - 4200, [logo], noBorders: true), title));
    }

    private static Table Boxes(ProformaModel m)
    {
        const int customer = 6066, gap = 200, label = 1500, value = ContentWidth - customer - gap - label;

        (string Label, string? Value)[] customerLines =
        [
            ("Name", m.ClientName), ("Project", m.ProjectName), ("Contact Number", m.ContactNumber), ("Location", m.Location),
        ];
        (string Label, string? Value)[] references =
        [
            ("Date#", ProformaLook.Date(m.Date)),
            ("PI No", m.Number ?? (m.Draft ? "DRAFT" : "Given on download")),
            ("Quote No#", m.QuoteNumber),
            ("Quote Date#", ProformaLook.Date(m.QuoteDate)),
            ("Ref#", m.Ref),
        ];

        var rows = new List<TableRow>();
        for (var i = 0; i < 5; i++)
        {
            var left = i == 0
                ? Cell(customer, [P(null, R("Customer Details:", bold: true))], fill: ProformaLook.HeadBg)
                : Cell(customer, [P(null, R($"{customerLines[i - 1].Label} : ", bold: true, color: ProformaLook.Muted),
                    R(ProformaLook.Dash(customerLines[i - 1].Value), bold: true))]);
            rows.Add(new TableRow(
                left,
                Cell(gap, [P(null)], noBorders: true),
                Cell(label, [P(null, R(references[i].Label, bold: true))], fill: ProformaLook.HeadBg),
                Cell(value, [P(JustificationValues.Right, R(ProformaLook.Dash(references[i].Value), bold: true))])));
        }
        return Grid([customer, gap, label, value], borders: false, [.. rows]);
    }

    private static Table Items(ProformaModel m)
    {
        int[] widths = [560, 3900, 1000, 1700, 600, 500, 1000, ContentWidth - 9260];
        string[] headings = ["SNO", "DESCRIPTION", "BRAND", "MODEL / PART NO.", "UNITS", "QTY", "UNIT PRICE", "TOTAL AMOUNT"];

        var header = new TableRow(new TableRowProperties(new TableHeader(), new CantSplit()));
        for (var c = 0; c < widths.Length; c++)
            header.Append(Cell(widths[c], [P(JustificationValues.Center, R(headings[c], bold: true, size: 14))], fill: ProformaLook.HeadBg));

        var rows = new List<TableRow> { header };
        foreach (var section in m.Sections)
        {
            if (!string.IsNullOrWhiteSpace(section.Title))
                rows.Add(new TableRow(new TableRowProperties(new CantSplit()),
                    Cell(ContentWidth, [P(JustificationValues.Center, R(section.Title.Trim().ToUpperInvariant(), bold: true, color: "000000", size: 17))],
                        fill: ProformaLook.SectionBg, span: widths.Length)));

            foreach (var line in section.Lines)
            {
                rows.Add(new TableRow(new TableRowProperties(new CantSplit()),
                    Cell(widths[0], [P(JustificationValues.Center, R(line.Number.ToString(), size: Small))]),
                    Cell(widths[1], [P(JustificationValues.Left, R(line.Description, size: Small))]),
                    Cell(widths[2], [P(JustificationValues.Center, R(line.Brand ?? string.Empty, size: Small))]),
                    Cell(widths[3], [P(JustificationValues.Center, R(line.Model ?? string.Empty, size: Small))]),
                    Cell(widths[4], [P(JustificationValues.Center, R(ProformaText.Unit(line.Unit), size: Small))]),
                    Cell(widths[5], [P(JustificationValues.Center, R(ProformaLook.Qty(line.Quantity), size: Small))]),
                    Cell(widths[6], [P(JustificationValues.Right, R(ProformaLook.Money(line.UnitPrice), size: Small))]),
                    Cell(widths[7], [P(JustificationValues.Right, R(ProformaLook.Money(line.Total), size: Small))])));
            }
        }

        if (rows.Count == 1)
            rows.Add(new TableRow(Cell(ContentWidth, [P(JustificationValues.Center, R("This quotation has no items.", color: ProformaLook.Muted))],
                span: widths.Length)));

        return Grid(widths, borders: true, [.. rows]);
    }

    private static Table Totals(ProformaModel m)
    {
        const int value = 1800, label = 2200, left = ContentWidth - value - label;

        var notes = new List<OpenXmlElement>();
        var po = ProformaLook.PoNote(m.PoNumber, m.PoDate);
        if (po is not null) notes.Add(P(null, R("Notes: ", bold: true), R(po, bold: true)));
        notes.Add(P(null, R("Amount In Words: ", bold: true), R(MoneyWords.English(m.NetTotal), italic: true)));

        (string Label, decimal Value, bool Strong)[] totals =
        [
            ("Subtotal", m.Subtotal, false), ("Discount", m.Discount, false), ("Net Total (QAR)", m.NetTotal, true),
        ];

        var rows = new List<TableRow>();
        for (var i = 0; i < totals.Length; i++)
        {
            var leftCell = i == 0
                ? Cell(left, [.. notes], noBorders: true, merge: MergedCellValues.Restart)
                : Cell(left, [P(null)], noBorders: true, merge: MergedCellValues.Continue);
            var size = totals[i].Strong ? 20 : Body;
            var fill = totals[i].Strong ? ProformaLook.HeadBg : null;
            rows.Add(new TableRow(
                leftCell,
                Cell(label, [P(JustificationValues.Right, R(totals[i].Label, bold: true, size: size))], fill: fill),
                Cell(value, [P(JustificationValues.Right, R(ProformaLook.Money(totals[i].Value), bold: true, size: size))], fill: fill)));
        }
        return Grid([left, label, value], borders: false, [.. rows]);
    }

    private static Table TermsAndPayment()
    {
        const int right = 4800, left = ContentWidth - right;

        var terms = new List<OpenXmlElement> { P(null, R("Terms & Conditions:", bold: true, color: ProformaLook.BrandStrong, size: 20)) };
        for (var i = 0; i < ProformaText.Terms.Count; i++)
            terms.Add(P(null, R($"{i + 1}. ", bold: true), R(ProformaText.Terms[i])));

        (string Label, string Value)[] payment =
        [
            ("Account Holder", CompanyPaymentDetails.AccountHolder),
            ("Account Number", CompanyPaymentDetails.AccountNumber),
            ("Bank", CompanyPaymentDetails.Bank),
            ("Branch", CompanyPaymentDetails.Branch),
            ("SWIFT Code", CompanyPaymentDetails.SwiftCode),
        ];
        const int payLabel = 1500, payValue = right - 200 - payLabel;
        var payTable = Grid([payLabel, payValue], borders: true, [.. payment.Select(p => new TableRow(
            Cell(payLabel, [P(null, R(p.Label, bold: true))], fill: ProformaLook.HeadBg),
            Cell(payValue, [P(null, R(p.Value, bold: true))])))]);

        var pay = new List<OpenXmlElement>
        {
            P(null, R("Payment details as follows:", bold: true, color: ProformaLook.BrandStrong, size: 20)),
            payTable,
            P(null),   // a table cell must end with a paragraph
        };

        return Grid([left, right], borders: false,
            new TableRow(Cell(left, [.. terms], noBorders: true), Cell(right, [.. pay], noBorders: true)));
    }

    private static IEnumerable<Paragraph> Company()
    {
        yield return P(JustificationValues.Center,
            new ParagraphBorders(new TopBorder { Val = BorderValues.Single, Size = 12, Color = ProformaLook.Brand, Space = 4 }),
            R(ProformaText.CompanyName, bold: true, color: ProformaLook.BrandStrong, size: 22));
        yield return P(JustificationValues.Center, R(ProformaText.CompanyAddress, color: ProformaLook.Muted, size: Small));
        yield return P(JustificationValues.Center, R($"Mobile {ProformaText.CompanyMobiles}", color: ProformaLook.Muted, size: Small));
    }

    // ---------- building blocks ----------

    private static void AddStyles(MainDocumentPart main)
    {
        var styles = main.AddNewPart<StyleDefinitionsPart>();
        styles.Styles = new Styles(new DocDefaults(
            new RunPropertiesDefault(new RunProperties(
                new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri", ComplexScript = "Calibri", EastAsia = "Calibri" },
                new Color { Val = ProformaLook.Ink },
                new FontSize { Val = Body.ToString() },
                new FontSizeComplexScript { Val = Body.ToString() })),
            new ParagraphPropertiesDefault(new ParagraphProperties(
                new SpacingBetweenLines { After = "0", Before = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto }))));
        styles.Styles.Save();
    }

    private static string AddFooter(MainDocumentPart main)
    {
        var part = main.AddNewPart<FooterPart>();
        part.Footer = new Footer(P(JustificationValues.Right,
            R("Page ", color: ProformaLook.Muted, size: 14),
            Field("PAGE"),
            R(" of ", color: ProformaLook.Muted, size: 14),
            Field("NUMPAGES")));
        part.Footer.Save();
        return main.GetIdOfPart(part);
    }

    private static SimpleField Field(string code) =>
        new(new Run(new RunProperties(new Color { Val = ProformaLook.Muted }, new FontSize { Val = "14" }), new Text("1")))
        { Instruction = $" {code} " };

    private static Drawing Logo(MainDocumentPart main)
    {
        var image = main.AddImagePart(ImagePartType.Png);
        using (var data = new MemoryStream(ProformaLook.Logo)) image.FeedData(data);
        var id = main.GetIdOfPart(image);

        var (w, h) = ProformaLook.LogoSize;
        const long cx = 1_750_000L;   // about 1.9 inches wide
        var cy = cx * h / Math.Max(1, w);

        return new Drawing(new DW.Inline(
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.DocProperties { Id = 1U, Name = "Jama Go logo" },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            new A.Graphic(new A.GraphicData(
                new PIC.Picture(
                    new PIC.NonVisualPictureProperties(
                        new PIC.NonVisualDrawingProperties { Id = 0U, Name = "jamago-logo.png" },
                        new PIC.NonVisualPictureDrawingProperties()),
                    new PIC.BlipFill(new A.Blip { Embed = id }, new A.Stretch(new A.FillRectangle())),
                    new PIC.ShapeProperties(
                        new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }),
                        new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
        {
            DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U,
        });
    }

    /// <summary>A run with its font, size and colour set on the run itself — not every viewer applies the
    /// document's defaults.</summary>
    private static Run R(string text, bool bold = false, bool italic = false, string? color = null, int? size = null)
    {
        var half = (size ?? Body).ToString();
        var props = new RunProperties(new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri", ComplexScript = "Calibri", EastAsia = "Calibri" });
        if (bold) props.Append(new Bold());
        if (italic) props.Append(new Italic());
        props.Append(new Color { Val = color ?? ProformaLook.Ink });
        props.Append(new FontSize { Val = half });
        props.Append(new FontSizeComplexScript { Val = half });
        return new Run(props, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    private static Paragraph P(JustificationValues? align, params OpenXmlElement[] runs) => P(align, null, runs);

    /// <summary>A paragraph with no spacing around it. Properties go in the order Word's schema requires
    /// (borders, spacing, justification) — Word calls a file damaged when they are out of order.</summary>
    private static Paragraph P(JustificationValues? align, ParagraphBorders? borders, params OpenXmlElement[] runs)
    {
        var props = new ParagraphProperties();
        if (borders is not null) props.Append(borders);
        props.Append(new SpacingBetweenLines { After = "0", Before = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto });
        if (align is { } a) props.Append(new Justification { Val = a });
        var p = new Paragraph(props);
        p.Append(runs);
        return p;
    }

    private static Paragraph Spacer(int twips) =>
        new(new ParagraphProperties(new SpacingBetweenLines { After = "0", Before = "0", Line = twips.ToString(), LineRule = LineSpacingRuleValues.Exact }));

    private static TableCell Cell(
        int width, OpenXmlElement[] content, string? fill = null, int span = 1, bool noBorders = false,
        string? borderColor = null, uint borderSize = 4, MergedCellValues? merge = null)
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
            var color = borderColor ?? ProformaLook.Border;
            props.Append(new TableCellBorders(
                new TopBorder { Val = BorderValues.Single, Size = borderSize, Color = color },
                new LeftBorder { Val = BorderValues.Single, Size = borderSize, Color = color },
                new BottomBorder { Val = BorderValues.Single, Size = borderSize, Color = color },
                new RightBorder { Val = BorderValues.Single, Size = borderSize, Color = color }));
        }
        if (fill is not null) props.Append(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = fill });
        props.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });

        var cell = new TableCell(props);
        cell.Append(content);
        return cell;
    }

    private static Table Grid(int[] widths, bool borders, params TableRow[] rows)
    {
        var props = new TableProperties(
            new TableWidth { Width = widths.Sum().ToString(), Type = TableWidthUnitValues.Dxa },
            new TableLayout { Type = TableLayoutValues.Fixed },
            new TableCellMarginDefault(
                new TopMargin { Width = "20", Type = TableWidthUnitValues.Dxa },
                new TableCellLeftMargin { Width = 80, Type = TableWidthValues.Dxa },
                new BottomMargin { Width = "20", Type = TableWidthUnitValues.Dxa },
                new TableCellRightMargin { Width = 80, Type = TableWidthValues.Dxa }));
        // No table-level borders either way: each cell carries its own (or none), which every viewer honours —
        // some viewers let a table's "no borders" hide the cells' own borders.
        _ = borders;

        var table = new Table(props, new TableGrid(widths.Select(w => new GridColumn { Width = w.ToString() })));
        table.Append(rows);
        return table;
    }
}
