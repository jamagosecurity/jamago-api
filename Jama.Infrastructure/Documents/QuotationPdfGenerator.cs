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
        // The totals and the closing sentence are set in Arabic as well as
        // English, and the face that draws them ships with the assembly.
        DocumentFonts.EnsureRegistered();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Ink).FontFamily(DocumentFonts.Body));

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
                    right.Item().Text("عرض سعر")
                        .FontFamily(DocumentFonts.Arabic).FontSize(12).FontColor(InkSoft);
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
        var discounted = model.SpecialDiscount > 0;

        container.Column(outer =>
        {
            outer.Item().Row(row =>
            {
                // Left half is deliberately empty: totals belong on the same side
                // as the amounts column they sum.
                row.RelativeItem();

                row.ConstantItem(272).Column(column =>
                {
                    column.Item().Element(x => TotalRow(
                        x, "Subtotal", "الإجمالي الفرعي", Money(model.Subtotal)));

                    if (model.DiscountTotal > 0)
                        column.Item().Element(x => TotalRow(
                            x, "Line discount", "خصم البنود", Minus(model.DiscountTotal)));

                    if (model.TaxTotal > 0)
                        column.Item().Element(x => TotalRow(
                            x, "Tax", "الضريبة", Money(model.TaxTotal)));

                    // The discount agreed on the finished quote earns two rows:
                    // the total it came off, then what it takes away, so the
                    // customer can follow the subtraction rather than being asked
                    // to trust it. Without one, that pair would only repeat the
                    // final line, so it is left out entirely.
                    if (discounted)
                    {
                        column.Item().Element(x => TotalRow(
                            x, "Total", "الإجمالي", Money(model.TotalBeforeDiscount)));

                        column.Item().Element(x => TotalRow(
                            x, "Discount", "الخصم", Minus(model.SpecialDiscount), accent: true));
                    }

                    column.Item().PaddingTop(4).Element(x => TotalRow(
                        x,
                        discounted ? "FINAL AMOUNT (QAR)" : "TOTAL (QAR)",
                        discounted ? "المبلغ النهائي (ر.ق)" : "الإجمالي (ر.ق)",
                        Money(model.GrandTotal),
                        emphasis: true));
                });
            });

            outer.Item().PaddingTop(9).Element(x => ComposeAmountInWords(x, model.GrandTotal));
        });
    }

    /// <summary>A subtracted amount, written the way a reader checks it: with the
    /// sign against the figure, not implied by the label.</summary>
    private static string Minus(decimal value) => "−" + Money(value);

    private static void TotalRow(
        IContainer container,
        string label,
        string labelAr,
        string value,
        bool emphasis = false,
        bool accent = false)
    {
        var box = emphasis
            ? container.Background(BrandStrong).Padding(8)
            : container.BorderBottom(1).BorderColor(Line).PaddingVertical(5).PaddingHorizontal(8);

        var valueColor = emphasis ? White : accent ? Accent : Ink;

        box.Row(row =>
        {
            row.RelativeItem().Column(text =>
            {
                text.Item().Text(label)
                    .FontSize(emphasis ? 10 : 9)
                    .Bold()
                    .FontColor(emphasis ? White : InkSoft);

                // Right-to-left on the cell, not the string: Arabic set in a
                // left-to-right flow puts its brackets on the wrong end of the
                // line. AlignLeft keeps the pair stacked against the same edge,
                // which reversing the direction would otherwise undo.
                text.Item().ContentFromRightToLeft().AlignLeft().Text(labelAr)
                    .FontFamily(DocumentFonts.Arabic)
                    .FontSize(emphasis ? 9 : 8)
                    .FontColor(emphasis ? White : Muted);
            });

            row.ConstantItem(100).AlignRight().AlignMiddle().Text(value)
                .FontSize(emphasis ? 13 : 9)
                .Bold()
                .FontColor(valueColor);
        });
    }

    /// <summary>
    /// The amount payable, spelled out in both languages.
    ///
    /// A figure can be altered after the document leaves here with one keystroke;
    /// the sentence has to be rewritten to agree with it, so the two together are
    /// what make the total hard to quietly change.
    /// </summary>
    private static void ComposeAmountInWords(IContainer container, decimal amount)
    {
        container.Border(1).BorderColor(PanelBorder).Background(PanelBg)
            .PaddingVertical(7).PaddingHorizontal(9)
            .Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(96).Text("AMOUNT IN WORDS")
                        .FontSize(7.5f).Bold().FontColor(Brand).LetterSpacing(0.06f);
                    row.RelativeItem().Text(MoneyWords.English(amount))
                        .FontSize(8.5f).Italic().FontColor(Ink);
                });

                // The whole line is reversed, label included: the Arabic sentence
                // opens with "فقط" and closes with "لا غير", and set left to right
                // it would read with those two ends swapped.
                column.Item().PaddingTop(4).ContentFromRightToLeft().Row(row =>
                {
                    row.ConstantItem(96).Text("المبلغ كتابةً")
                        .FontFamily(DocumentFonts.Arabic)
                        .FontSize(8f).Bold().FontColor(Brand);
                    row.RelativeItem().Text(MoneyWords.Arabic(amount))
                        .FontFamily(DocumentFonts.Arabic)
                        .FontSize(9f).FontColor(Ink);
                });
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
