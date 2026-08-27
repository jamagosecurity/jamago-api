using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Jama.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Jama.Infrastructure.Documents;

/// <summary>
/// Renders a bill of quantities as a branded PDF.
///
/// Shares the palette and embedded logo with the invoice and quotation
/// generators so everything a client receives looks like one company. The
/// layout is its own: a BOQ is read section by section, with a subtotal at the
/// foot of each, which neither of the others needs.
/// </summary>
public sealed class BoqPdfGenerator : IBoqPdfGenerator
{
    private static readonly Color Brand = Color.FromHex("#2594D2");
    private static readonly Color BrandStrong = Color.FromHex("#1A7BB5");
    private static readonly Color Accent = Color.FromHex("#C2660F");
    private static readonly Color Ink = Color.FromHex("#0F2846");
    private static readonly Color InkSoft = Color.FromHex("#3E5872");
    private static readonly Color Muted = Color.FromHex("#7C93A6");
    private static readonly Color PanelBg = Color.FromHex("#F2F8FC");
    private static readonly Color PanelBorder = Color.FromHex("#DCEAF4");
    private static readonly Color SectionBg = Color.FromHex("#EAF3FA");
    /// <summary>Alternate-row tint, deliberately part-transparent so the
    /// watermark beneath still reads through it.</summary>
    private static readonly Color RowAlt = Color.FromHex("#66F2F8FC");
    private static readonly Color Line = Color.FromHex("#E3EBF1");
    private static readonly Color White = Color.FromHex("#FFFFFF");

    private static readonly byte[] LogoBytes =
        LoadEmbedded("Jama.Infrastructure.Documents.Assets.jamago-logo.png");

    /// <summary>The tall quote lockup, pre-faded to the same ~10% the invoice
    /// watermark uses. QuestPDF has no opacity element, so the fade is baked into
    /// the asset rather than applied at render time.</summary>
    private static readonly byte[] WatermarkBytes =
        LoadEmbedded("Jama.Infrastructure.Documents.Assets.jamago-quote-watermark.png");

    /// <summary>
    /// Maker's marks, keyed by every spelling that should resolve to one.
    ///
    /// Mirrors CAMERA_BRANDS in the Angular client, deliberately: the quotation
    /// on screen and the quotation in the client's hand have to show the same
    /// logo for the same brand. A brand with no artwork here simply prints its
    /// name, which is why the fallback is not an error.
    /// </summary>
    private static readonly Dictionary<string, byte[]> BrandMarks = BuildBrandMarks();

    private static Dictionary<string, byte[]> BuildBrandMarks()
    {
        var files = new (string File, string[] Aliases)[]
        {
            ("hikvision", ["hikvision", "hik"]),
            ("dahua", ["dahua", "dahua technology"]),
            ("uniview", ["uniview", "unv"]),
            ("tiandy", ["tiandy"]),
        };

        var marks = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var (file, aliases) in files)
        {
            var bytes = LoadEmbedded($"Jama.Infrastructure.Documents.Assets.brands.{file}.png");
            if (bytes.Length == 0) continue;

            foreach (var alias in aliases)
                marks[alias] = bytes;
        }

        return marks;
    }

    private static byte[]? BrandMark(string? brand) =>
        !string.IsNullOrWhiteSpace(brand) && BrandMarks.TryGetValue(brand.Trim(), out var bytes)
            ? bytes
            : null;

    private static byte[] LoadEmbedded(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (stream is null) return [];

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    /// <summary>How long a quotation stands. Two weeks, counted from the issue
    /// date — long enough for a client to decide, short enough that supplier
    /// prices have not moved underneath us.</summary>
    private const int ValidityDays = 14;

    /// <summary>Matches an email inside a display name, capturing only the part
    /// before the "@".</summary>
    private static readonly Regex EmbeddedEmail =
        new(@"<\s*([^@<>\s]+)@[^<>]*>", RegexOptions.Compiled);

    /// <summary>"Jane Doe &lt;jane@jamago.qa&gt;" becomes "Jane Doe (jane)". The
    /// domain is ours and identical on every quotation, so printing it on a
    /// client-facing document adds a line of noise and no information.</summary>
    private static string? PreparedBy(string? value) =>
        string.IsNullOrWhiteSpace(value) ? value : EmbeddedEmail.Replace(value, "($1)").Trim();

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

    private static string Date(DateOnly value) =>
        value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>Trims a trailing ".00" so quantities read "12" not "12.00", while
    /// "2.5" keeps its half — cable is measured in metres.</summary>
    private static string Qty(decimal value) =>
        value == Math.Floor(value)
            ? ((long)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);

    public byte[] Generate(BoqPdfModel model)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Ink).FontFamily(Fonts.Calibri));

                page.Background().Element(ComposeWatermark);
                page.Header().Element(header => ComposeHeader(header, model));
                page.Content().PaddingTop(14).Element(content => ComposeContent(content, model));
                page.Footer().Element(footer => ComposeFooter(footer, model));
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, BoqPdfModel model)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                if (LogoBytes.Length > 0)
                    row.ConstantItem(150).AlignMiddle().Image(LogoBytes).FitWidth();
                else
                    row.RelativeItem().Text("JAMA GO").FontSize(20).Bold().FontColor(Brand);

                row.RelativeItem().AlignRight().Column(right =>
                {
                    right.Item().Text("QUOTATION")
                        .FontSize(24).Bold().FontColor(BrandStrong);
                    // No status line: a quote handed to a client is the
                    // offer itself. "DRAFT" is our workflow, not their concern.
                    right.Item().PaddingTop(3).Text(text =>
                    {
                        text.Span("Quotation no  ").FontSize(9).FontColor(Muted);
                        text.Span(model.BoqNumber).FontSize(12).Bold().FontColor(Ink);
                    });
                });
            });

            column.Item().PaddingTop(10).Height(3).Background(Brand);
        });
    }

    private static void ComposeContent(IContainer container, BoqPdfModel model)
    {
        container.Column(column =>
        {
            column.Item().Element(x => ComposeDetails(x, model));

            foreach (var section in model.Sections)
                column.Item().PaddingTop(12).Element(x => ComposeSection(x, section));

            column.Item().PaddingTop(14).Element(x => ComposeTotal(x, model));

            if (!string.IsNullOrWhiteSpace(model.Notes))
                column.Item().PaddingTop(12).Element(x => ComposeNotes(x, model.Notes!));
        });
    }

    /// <summary>
    /// Who the quote is for and what it covers, as a full-width table.
    ///
    /// Two label/value pairs per row rather than one column down the right: six
    /// short values stacked in half the page left the other half blank and forced
    /// the longest of them to wrap. Client facts run down the left, document
    /// facts down the right.
    ///
    /// It sits in the content rather than the page header so it prints once. On a
    /// quote that runs to a second page, repeating the client's phone number
    /// above every table costs a third of the page and tells nobody anything.
    /// </summary>
    private static void ComposeDetails(IContainer container, BoqPdfModel model)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(94);   // label
                columns.RelativeColumn(1);    // value
                columns.ConstantColumn(94);   // label
                // Slightly wider: "Prepared by" carries a name and an email.
                columns.RelativeColumn(1.15f);
            });

            DetailPair(table, "To", model.ClientName, "Date", Date(model.IssueDate));
            DetailPair(table, "Project", model.ProjectName, "Location", model.SiteLocation);
            DetailPair(table, "Contact number", model.ContactNumber, "Prepared by", PreparedBy(model.PreparedByName));

            // Spans the remaining three columns: the date alone means nothing
            // without the term beside it, and the two must not be split apart.
            DetailLabel(table.Cell(), "Valid until");
            DetailCell(table.Cell().ColumnSpan(3)).Text(text =>
            {
                text.Span(Date(model.IssueDate.AddDays(ValidityDays)))
                    .FontSize(9).Bold().FontColor(Accent);
                text.Span($"     Valid for {ValidityDays} days (2 weeks) from the date above.")
                    .FontSize(8).FontColor(Muted);
            });
        });
    }

    private static void DetailPair(
        TableDescriptor table,
        string leftLabel,
        string? leftValue,
        string rightLabel,
        string? rightValue)
    {
        DetailLabel(table.Cell(), leftLabel);
        DetailValue(table.Cell(), leftValue);
        DetailLabel(table.Cell(), rightLabel);
        DetailValue(table.Cell(), rightValue);
    }

    /// <summary>Background BEFORE the cell chain: applied after the padding it
    /// tints only the text box, leaving a short bar floating in the cell.</summary>
    private static void DetailLabel(IContainer container, string label) =>
        DetailCell(container.Background(PanelBg))
            .Text(label.ToUpperInvariant())
            .FontSize(6.8f).Bold().FontColor(Muted).LetterSpacing(0.08f);

    /// <summary>An empty value still prints its label with a dash, so a missing
    /// contact number reads as "not given" rather than as a field the document
    /// never had. No fill: the watermark shows through these cells.</summary>
    private static void DetailValue(IContainer container, string? value) =>
        DetailCell(container)
            .Text(string.IsNullOrWhiteSpace(value) ? "\u2014" : value)
            .FontSize(9).Bold().FontColor(Ink);

    private static IContainer DetailCell(IContainer container) =>
        container.Border(1).BorderColor(PanelBorder)
            .PaddingVertical(6).PaddingHorizontal(9)
            .AlignMiddle();

    private static void ComposeSection(IContainer container, BoqPdfSection section)
    {
        container.Column(column =>
        {
            // Section heading doubles as the table's caption, so a section that
            // breaks across pages still says what it is.
            // Three tracks with matching outer widths, so the title sits dead
            // centre of the band however long the subtotal beside it runs. A
            // centred RelativeItem would be pushed off by the subtotal's width.
            column.Item().Background(SectionBg).Padding(7)
                .AlignCenter()
                .Text($"{section.Number}.  {section.Title}")
                .FontSize(10).Bold().FontColor(BrandStrong);

            column.Item().Table(table =>
            {
                // The client's own layout: image, brand, model and description
                // each get a column instead of being stacked into one cell.
                // Page is A4 less 28pt margins = 539pt, so the fixed columns
                // total 362 and description takes what is left.
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);   // s.no
                    columns.ConstantColumn(54);   // brand
                    columns.ConstantColumn(46);   // image
                    columns.ConstantColumn(66);   // model
                    columns.RelativeColumn(1);    // description
                    columns.ConstantColumn(32);   // unit
                    columns.ConstantColumn(28);   // qty
                    columns.ConstantColumn(52);   // unit price
                    columns.ConstantColumn(62);   // total
                });

                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "S.No", false);
                    HeaderCell(header.Cell(), "Brand", false);
                    HeaderCell(header.Cell(), "Image", false);
                    HeaderCell(header.Cell(), "Model", false);
                    HeaderCell(header.Cell(), "Description", false);
                    HeaderCell(header.Cell(), "Unit", true);
                    HeaderCell(header.Cell(), "Qty", true);
                    HeaderCell(header.Cell(), "Unit Price", true);
                    HeaderCell(header.Cell(), "Total Amount", true);
                });

                var index = 0;
                foreach (var line in section.Lines)
                {
                    // Zebra striping so a long section stays readable across a row.
                    // Odd rows only: an opaque white fill on the rest would blank
                    // out the watermark behind the table.
                    var shaded = index++ % 2 == 1;

                    Body(table.Cell(), shaded).Text(line.Number).FontColor(Muted);

                    // Fixed height whether or not there is a picture, so rows in
                    // the same section line up rather than jumping about.
                    // The maker's mark where there is one, the name where there
                    // is not — most of a real catalogue is cable and brackets
                    // from suppliers whose logo nobody has on file.
                    var brandCell = Body(table.Cell(), shaded).AlignMiddle();
                    if (BrandMark(line.Brand) is { } mark)
                        // FitArea, not FitHeight: a wide wordmark scaled to a
                        // fixed height overflows a 54pt column, which QuestPDF
                        // reports as conflicting size constraints rather than
                        // clipping. Bounded both ways, it just gets smaller.
                        brandCell.Height(18).AlignLeft().Image(mark).FitArea();
                    else
                        brandCell.Text(Dash(line.Brand)).FontColor(InkSoft);

                    // Fixed box, as the editor has: a source photo may be
                    // 4000px wide or 200px tall and every row must still show
                    // the same size picture. FitArea contains it inside the box
                    // rather than cropping the camera out of its own thumbnail.
                    var photo = Body(table.Cell(), shaded).Height(38).AlignMiddle();
                    if (line.Image is { Length: > 0 })
                        photo.AlignCenter().Image(line.Image).FitArea();
                    else
                        photo.AlignCenter().Text(EmDash).FontSize(9).FontColor(Muted);
                    Body(table.Cell(), shaded).Text(Dash(line.ModelNo)).FontSize(8).FontColor(InkSoft);

                    Body(table.Cell(), shaded).Column(cell =>
                    {
                        cell.Item().Text(line.ItemName).Bold().FontColor(Ink);

                        if (!string.IsNullOrWhiteSpace(line.Description))
                            cell.Item().PaddingTop(1).Text(line.Description!)
                                .FontSize(7.5f).FontColor(Muted);

                        // Right-to-left on the cell, not the string: Arabic set in
                        // a left-to-right flow puts its punctuation on the wrong
                        // end of the line.
                        if (!string.IsNullOrWhiteSpace(line.DescriptionAr))
                            cell.Item().PaddingTop(1)
                                .ContentFromRightToLeft()
                                .Text(line.DescriptionAr!)
                                .FontSize(7.5f).FontColor(Muted);
                    });

                    Body(table.Cell(), shaded).AlignRight().Text(Unit(line.Uom)).FontColor(InkSoft);
                    Body(table.Cell(), shaded).AlignRight().Text(Qty(line.Quantity));
                    Body(table.Cell(), shaded).AlignRight().Text(Money(line.UnitRate));
                    Body(table.Cell(), shaded).AlignRight().Text(Money(line.LineTotal)).Bold();
                }

                // The section's money under Total Amount, where a reader adding
                // the rows up arrives at it — not beside the heading.
                table.Cell().ColumnSpan(8)
                    .BorderTop(1).BorderColor(PanelBorder)
                    .PaddingVertical(6).PaddingHorizontal(5)
                    .AlignRight()
                    .Text("Section total")
                    .FontSize(7.5f).Bold().FontColor(Muted).LetterSpacing(0.06f);

                table.Cell()
                    .BorderTop(1).BorderColor(PanelBorder)
                    .PaddingVertical(6).PaddingHorizontal(5)
                    .AlignRight()
                    .Text(Money(section.Subtotal))
                    .FontSize(9.5f).Bold().FontColor(BrandStrong);
            });
        });
    }

    private const string EmDash = "\u2014";

    /// <summary>
    /// How a unit is written on the document.
    ///
    /// The enum member stays "Piece" — it is the stored value, and renaming it
    /// would need a data migration — so only the printed form changes.
    /// </summary>
    private static string Unit(string uom) =>
        string.Equals(uom, "Piece", StringComparison.OrdinalIgnoreCase) ? "pcs" : uom;

    /// <summary>An em dash for an empty cell, so a blank reads as "not recorded"
    /// rather than as a column that failed to print.</summary>
    private static string Dash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? EmDash : value;

    private static void HeaderCell(IContainer container, string text, bool alignRight)
    {
        var cell = container.Background(BrandStrong).PaddingVertical(5).PaddingHorizontal(5);
        var styled = alignRight ? cell.AlignRight() : cell;
        styled.Text(text).FontSize(7).Bold().FontColor(White);
    }

    private static IContainer Body(IContainer container, bool shaded) =>
        (shaded ? container.Background(RowAlt) : container)
            .BorderBottom(1).BorderColor(Line)
            .PaddingVertical(4).PaddingHorizontal(5);

    private static void ComposeTotal(IContainer container, BoqPdfModel model)
    {
        container.Row(row =>
        {
            // Left half stays empty: the total belongs on the same side as the
            // amounts column it sums.
            row.RelativeItem();

            row.ConstantItem(260).Background(BrandStrong).Padding(9).Row(inner =>
            {
                inner.RelativeItem().Text("TOTAL (QAR)")
                    .FontSize(10).Bold().FontColor(White);
                inner.ConstantItem(110).AlignRight().Text(Money(model.Total))
                    .FontSize(13).Bold().FontColor(White);
            });
        });
    }

    private static void ComposeNotes(IContainer container, string body)
    {
        container.Border(1).BorderColor(PanelBorder).Padding(9).Column(column =>
        {
            column.Item().PaddingBottom(3).Text("NOTES")
                .FontSize(7.5f).Bold().FontColor(Brand).LetterSpacing(0.09f);
            column.Item().Text(body).FontColor(InkSoft);
        });
    }

    /// <summary>The logo, centred on every page, on the BACKGROUND layer — it sits
    /// under the content, not over it. That only works because the table rows no
    /// longer paint an opaque fill (see <see cref="Body"/>); the solid bands that
    /// remain — section headings, column headers, the total — hide it where they
    /// fall, which is what being behind something means. The layer does not
    /// displace content: the tables lay out exactly as they would without it.</summary>
    private static void ComposeWatermark(IContainer container)
    {
        if (WatermarkBytes.Length == 0) return;

        // Sized by width, not height: this lockup is portrait (roughly 1:1.56),
        // so 300pt across lands it about 470pt tall — centred on A4 with room
        // to spare at the head and foot.
        container.AlignCenter().AlignMiddle().Width(300).Image(WatermarkBytes).FitWidth();
    }

    private static void ComposeFooter(IContainer container, BoqPdfModel model)
    {
        container.Column(column =>
        {
            column.Item().PaddingBottom(5).Height(1).Background(Line);
            column.Item().Row(row =>
            {
                row.RelativeItem().Text($"Jama Go Security Equipment · Doha, Qatar · {model.BoqNumber}")
                    .FontSize(7.5f).FontColor(Muted);

                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Muted));
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        });
    }
}
