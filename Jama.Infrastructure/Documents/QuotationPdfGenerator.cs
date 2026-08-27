using System.Globalization;
using System.Reflection;
using Jama.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Jama.Infrastructure.Documents;

/// <summary>
/// Renders a quotation as a branded PDF.
///
/// Shares the palette and the embedded logo with <see cref="InvoicePdfGenerator"/>
/// so the two documents a customer receives look like they came from the same
/// company. Kept as its own class rather than a mode on the invoice generator:
/// the layouts have almost nothing in common beyond the header.
/// </summary>
public sealed class QuotationPdfGenerator : IQuotationPdfGenerator
{
    private static readonly Color Brand = Color.FromHex("#2594D2");
    private static readonly Color BrandStrong = Color.FromHex("#1A7BB5");
    private static readonly Color Accent = Color.FromHex("#F6993D");
    private static readonly Color Ink = Color.FromHex("#0F2846");
    private static readonly Color InkSoft = Color.FromHex("#3E5872");
    private static readonly Color Muted = Color.FromHex("#7C93A6");
    private static readonly Color PanelBg = Color.FromHex("#F2F8FC");
    private static readonly Color PanelBorder = Color.FromHex("#DCEAF4");
    private static readonly Color RowAlt = Color.FromHex("#F7FAFC");
    private static readonly Color Line = Color.FromHex("#E3EBF1");
    private static readonly Color White = Color.FromHex("#FFFFFF");

    private static readonly byte[] LogoBytes =
        LoadEmbedded("Jama.Infrastructure.Documents.Assets.jamago-logo.png");

    private static byte[] LoadEmbedded(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (stream is null) return [];

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    /// <summary>Money as it is written on a Qatari quotation.</summary>
    private static string Money(decimal value) =>
        value.ToString("N2", CultureInfo.InvariantCulture);

    private static string Date(DateOnly value) =>
        value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>Trims a trailing ".00" so quantities read "3" rather than "3.00",
    /// while "2.5 metres" keeps its half.</summary>
    private static string Qty(decimal value) =>
        value == Math.Floor(value)
            ? ((long)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);

    public byte[] Generate(QuotationPdfModel model)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Ink).FontFamily(Fonts.Calibri));

                page.Header().Element(header => ComposeHeader(header, model));
                page.Content().PaddingTop(14).Element(content => ComposeContent(content, model));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, QuotationPdfModel model)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                if (LogoBytes.Length > 0)
                {
                    row.ConstantItem(150).AlignMiddle().Image(LogoBytes).FitWidth();
                }
                else
                {
                    row.RelativeItem().Text("JAMA GO").FontSize(20).Bold().FontColor(Brand);
                }

                row.RelativeItem().AlignRight().Column(right =>
                {
                    right.Item().Text("QUOTATION")
                        .FontSize(21).Bold().FontColor(BrandStrong).LetterSpacing(0.06f);
                    right.Item().PaddingTop(2).Text(model.QuoteNumber)
                        .FontSize(11).Bold().FontColor(Ink);
                    right.Item().PaddingTop(1).Text(model.Status.ToUpperInvariant())
                        .FontSize(8).Bold().FontColor(Accent).LetterSpacing(0.08f);
                });
            });

            // The brand rule under the header, matching the invoice.
            column.Item().PaddingTop(10).Height(3).Background(Brand);
        });
    }

    private static void ComposeContent(IContainer container, QuotationPdfModel model)
    {
        container.Column(column =>
        {
            column.Item().Element(x => ComposeParties(x, model));
            column.Item().PaddingTop(14).Element(x => ComposeLines(x, model));
            column.Item().PaddingTop(12).Element(x => ComposeTotals(x, model));

            if (!string.IsNullOrWhiteSpace(model.Notes))
                column.Item().PaddingTop(12).Element(x => ComposeNote(x, "Notes", model.Notes!));

            if (!string.IsNullOrWhiteSpace(model.Terms))
                column.Item().PaddingTop(8).Element(x => ComposeNote(x, "Terms & conditions", model.Terms!));
        });
    }

    private static void ComposeParties(IContainer container, QuotationPdfModel model)
    {
        container.Row(row =>
        {
            row.RelativeItem().Element(box => Panel(box, "QUOTATION FOR", inner =>
            {
                inner.Item().Text(model.CustomerName).FontSize(11).Bold().FontColor(Ink);

                if (!string.IsNullOrWhiteSpace(model.CustomerCompany))
                    inner.Item().Text(model.CustomerCompany!).FontColor(InkSoft);

                if (!string.IsNullOrWhiteSpace(model.CustomerAddress))
                    inner.Item().PaddingTop(2).Text(model.CustomerAddress!).FontColor(InkSoft);

                if (!string.IsNullOrWhiteSpace(model.CustomerEmail))
                    inner.Item().PaddingTop(2).Text(model.CustomerEmail!).FontColor(Muted);

                if (!string.IsNullOrWhiteSpace(model.CustomerPhone))
                    inner.Item().Text(model.CustomerPhone!).FontColor(Muted);
            }));

            row.ConstantItem(12);

            row.RelativeItem().Element(box => Panel(box, "DETAILS", inner =>
            {
                inner.Item().Element(x => KeyValue(x, "Issue date", Date(model.IssueDate)));
                inner.Item().Element(x => KeyValue(
                    x, "Valid until", model.ValidUntil.HasValue ? Date(model.ValidUntil.Value) : "—"));
                inner.Item().Element(x => KeyValue(x, "Currency", "QAR"));
                inner.Item().Element(x => KeyValue(x, "Items", model.Lines.Count.ToString()));
            }));
        });
    }

    private static void Panel(IContainer container, string title, Action<ColumnDescriptor> body)
    {
        container
            .Border(1).BorderColor(PanelBorder).Background(PanelBg)
            .Padding(10)
            .Column(column =>
            {
                column.Item().PaddingBottom(4).Text(title)
                    .FontSize(7.5f).Bold().FontColor(Brand).LetterSpacing(0.09f);
                body(column);
            });
    }

    private static void KeyValue(IContainer container, string label, string value)
    {
        container.PaddingVertical(1).Row(row =>
        {
            row.ConstantItem(74).Text(label).FontColor(Muted);
            row.RelativeItem().Text(value).Bold().FontColor(Ink);
        });
    }

    private static void ComposeLines(IContainer container, QuotationPdfModel model)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(22);   // #
                columns.RelativeColumn(4);    // description
                columns.ConstantColumn(42);   // qty
                columns.ConstantColumn(62);   // rate
                columns.ConstantColumn(40);   // disc
                columns.ConstantColumn(40);   // tax
                columns.ConstantColumn(70);   // total
            });

            table.Header(header =>
            {
                HeaderCell(header.Cell(), "#", TextHorizontalAlignment.Left);
                HeaderCell(header.Cell(), "Description", TextHorizontalAlignment.Left);
                HeaderCell(header.Cell(), "Qty", TextHorizontalAlignment.Right);
                HeaderCell(header.Cell(), "Rate", TextHorizontalAlignment.Right);
                HeaderCell(header.Cell(), "Disc %", TextHorizontalAlignment.Right);
                HeaderCell(header.Cell(), "Tax %", TextHorizontalAlignment.Right);
                HeaderCell(header.Cell(), "Amount", TextHorizontalAlignment.Right);
            });

            foreach (var line in model.Lines)
            {
                // Zebra striping so a long quote stays readable across a row.
                var background = line.Number % 2 == 0 ? RowAlt : White;

                Body(table.Cell(), background).Text(line.Number.ToString()).FontColor(Muted);

                Body(table.Cell(), background).Column(cell =>
                {
                    cell.Item().Text(line.ItemName).Bold().FontColor(Ink);

                    var meta = new[] { line.Brand, line.ModelNo }
                        .Where(x => !string.IsNullOrWhiteSpace(x));
                    if (meta.Any())
                        cell.Item().Text(string.Join(" · ", meta)).FontSize(8).FontColor(Muted);

                    if (!string.IsNullOrWhiteSpace(line.Description))
                        cell.Item().Text(line.Description!).FontSize(8).FontColor(InkSoft);
                });

                Body(table.Cell(), background).AlignRight().Text(Qty(line.Quantity));
                Body(table.Cell(), background).AlignRight().Text(Money(line.UnitRate));
                Body(table.Cell(), background).AlignRight()
                    .Text(line.DiscountPercent == 0 ? "—" : Qty(line.DiscountPercent))
                    .FontColor(line.DiscountPercent == 0 ? Muted : Ink);
                Body(table.Cell(), background).AlignRight()
                    .Text(line.TaxPercent == 0 ? "—" : Qty(line.TaxPercent))
                    .FontColor(line.TaxPercent == 0 ? Muted : Ink);
                Body(table.Cell(), background).AlignRight().Text(Money(line.LineTotal)).Bold();
            }
        });
    }

    private static void HeaderCell(IContainer container, string text, TextHorizontalAlignment align)
    {
        var cell = container.Background(BrandStrong).PaddingVertical(6).PaddingHorizontal(5);
        var styled = align == TextHorizontalAlignment.Right ? cell.AlignRight() : cell;
        styled.Text(text).FontSize(8).Bold().FontColor(White).LetterSpacing(0.05f);
    }

    private static IContainer Body(IContainer container, Color background) =>
        container.Background(background).BorderBottom(1).BorderColor(Line)
            .PaddingVertical(5).PaddingHorizontal(5);

    private static void ComposeTotals(IContainer container, QuotationPdfModel model)
    {
        container.Row(row =>
        {
            // Left half is deliberately empty: totals belong on the same side as
            // the amounts column they sum.
            row.RelativeItem();

            row.ConstantItem(250).Column(column =>
            {
                column.Item().Element(x => TotalRow(x, "Subtotal", Money(model.Subtotal), false));

                if (model.DiscountTotal > 0)
                    column.Item().Element(x => TotalRow(x, "Discount", "-" + Money(model.DiscountTotal), false));

                if (model.TaxTotal > 0)
                    column.Item().Element(x => TotalRow(x, "Tax", Money(model.TaxTotal), false));

                column.Item().PaddingTop(4).Element(x => TotalRow(x, "TOTAL (QAR)", Money(model.GrandTotal), true));
            });
        });
    }

    private static void TotalRow(IContainer container, string label, string value, bool emphasis)
    {
        var box = emphasis
            ? container.Background(BrandStrong).Padding(8)
            : container.BorderBottom(1).BorderColor(Line).PaddingVertical(5).PaddingHorizontal(8);

        box.Row(row =>
        {
            row.RelativeItem().Text(label)
                .FontSize(emphasis ? 10 : 9)
                .Bold()
                .FontColor(emphasis ? White : InkSoft);

            row.ConstantItem(100).AlignRight().Text(value)
                .FontSize(emphasis ? 12 : 9)
                .Bold()
                .FontColor(emphasis ? White : Ink);
        });
    }

    private static void ComposeNote(IContainer container, string title, string body)
    {
        container.Border(1).BorderColor(PanelBorder).Padding(9).Column(column =>
        {
            column.Item().PaddingBottom(3).Text(title)
                .FontSize(7.5f).Bold().FontColor(Brand).LetterSpacing(0.09f);
            column.Item().Text(body).FontColor(InkSoft);
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().PaddingBottom(5).Height(1).Background(Line);
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Jama Go Security Equipment · Doha, Qatar")
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
