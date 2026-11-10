using System.Reflection;
using System.Text.RegularExpressions;
using Jama.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Jama.Infrastructure.Documents;

/// <summary>
/// The signed contract a client receives once their quotation is approved —
/// the company's full Arabic supply-and-installation contract (ten numbered
/// "البند" clauses: references, scope, payment schedule, duration, warranty,
/// changes, cancellation, liability, jurisdiction, copies), not the shorter
/// bilingual bullet summary this document used to be.
///
/// Renders whatever <see cref="ContractPdfModel.Body"/> holds — staff's own
/// edited wording, or IContractTextTemplate's default — as flowing RTL text,
/// with one convention carried over from ComposeFreeformTerms in
/// BoqPdfGenerator: a line starting "البند" is a clause heading, a blank line
/// is a paragraph break, everything else is a plain paragraph. There is no
/// further structure to trust once someone has been free to edit the text.
/// Pages are not fixed — QuestPDF paginates the body automatically — except
/// for the signature block, appended fixed at the end.
/// </summary>
public sealed class ContractPdfGenerator : IContractPdfGenerator
{
    // One rule for the whole document: all running text is black, and every heading, table header and the
    // footer band use the same single blue. Nothing else is coloured.
    private static readonly Color Ink = Color.FromHex("#000000");
    private static readonly Color Blue = Color.FromHex("#1B4F9C");
    private static readonly Color Line = Color.FromHex("#C9D6EA");
    private static readonly Color SectionBg = Color.FromHex("#EAF0FA");
    private static readonly Color White = Color.FromHex("#FFFFFF");
    private static readonly Color FooterBg = Blue;
    private static readonly Color Brand = Blue;
    private static readonly Color Accent = Blue;
    private static readonly Color InkSoft = Ink;

    private const string CompanyName = "JAMA GO SECURITY EQUIPMENTS";
    private const string CompanyNameAr = "شركة جاما جو للمعدات الأمنية";
    private const string CompanyPhone1 = "+974 4001 3599";
    private const string CompanyPhone2 = "+974 3064 4006";
    private const string CompanyEmail = "info@jamago.com";
    private const string CompanyAddress =
        "Showroom, Unit 41, Building 349, St 340, Zone 56, Al Ain Complex, Salwa Road";

    private static readonly byte[] LogoBytes =
        LoadEmbedded("Jama.Infrastructure.Documents.Assets.jamago-logo.png");

    /// <summary>The same pre-faded ~10% lockup BoqPdfGenerator watermarks
    /// every quotation page with — a signed contract is exactly the kind of
    /// document that benefits from the same quiet mark of authenticity.</summary>
    private static readonly byte[] WatermarkBytes =
        LoadEmbedded("Jama.Infrastructure.Documents.Assets.jamago-quote-watermark.png");

    private static byte[] LoadEmbedded(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (stream is null) return [];
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    /// <summary>A line starting "البند" — a clause heading, same convention as
    /// NumberedHeading in BoqPdfGenerator.</summary>
    private static readonly Regex ClauseHeading = new(@"^البند\s+\S", RegexOptions.Compiled);

    /// <summary>A line starting "- " — the payment schedule's own convention
    /// in IContractTextTemplate.BuildDefault, rendered as a bullet rather
    /// than a plain paragraph.</summary>
    private static readonly Regex BulletLine = new(@"^-\s+", RegexOptions.Compiled);

    /// <summary>One instalment line of the payment clause, exactly as the default wording writes it:
    /// "- الدفعة الأولى 20% بمبلغ 449,219.40 ريال قطري: دفعة مقدمة عند توقيع العقد."
    /// Groups: name, percent, amount, when it falls due. Anything staff reword so this no longer matches
    /// simply prints as ordinary bullets — the table is a nicer view of the text, never a replacement for it.</summary>
    private static readonly Regex PaymentRow = new(
        @"^-\s+(الدفعة\s+\S+)\s+(\d+(?:[.,]\d+)?)\s*%\s+بمبلغ\s+([\d,]+(?:\.\d+)?)\s+ريال\s+قطري\s*[:：]\s*(.+?)\.?$",
        RegexOptions.Compiled);

    public byte[] Generate(ContractPdfModel model)
    {
        DocumentFonts.EnsureRegistered();

        var paragraphs = SplitParagraphs(model.Body);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                // Generous, even margins: 15 mm at the sides, a little less above and below, so the
                // text never crowds the paper edge when the document is printed or bound.
                page.MarginHorizontal(44);
                page.MarginTop(30);
                page.MarginBottom(24);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor(Ink).FontFamily(DocumentFonts.Arabic));

                page.Header().Element(header => ComposeHeader(header, model));
                page.Background().Element(ComposeWatermark);
                page.Content().PaddingTop(10).Column(content =>
                {
                    var first = true;
                    foreach (var paragraph in paragraphs)
                    {
                        // The title and the quoted system description open the
                        // document centered — everything after reads as the
                        // body of the agreement, right-aligned like the rest.
                        if (first)
                        {
                            content.Item().PaddingTop(4).Column(title =>
                            {
                                // The title is the first thing read: large, heavy, blue, with a thick rule under it.
                                title.Item().AlignCenter().Text(paragraph)
                                    .FontSize(24).ExtraBold().FontColor(Blue);
                                title.Item().PaddingTop(6).AlignCenter().Width(150).Height(3).Background(Blue);
                                if (!string.IsNullOrWhiteSpace(model.ProjectName))
                                    title.Item().PaddingTop(10).AlignCenter().Text(model.ProjectName.Trim())
                                        .FontSize(15).Bold().FontColor(Ink);
                            });
                            first = false;
                            continue;
                        }

                        content.Item().PaddingTop(10).Element(x => ComposeParagraph(x, paragraph));
                    }

                    // The price schedule starts on a fresh page, so the amounts read as one block rather than
                    // being split from their heading by the end of a clause.
                    if (model.Prices is { Sections.Count: > 0 } prices)
                    {
                        content.Item().PageBreak();
                        content.Item().Element(x => ComposePrices(x, prices));
                    }

                    content.Item().PaddingTop(28).Element(x => ComposeSignatures(x));
                });
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    /// <summary>A faint centred lockup behind every page, same as
    /// BoqPdfGenerator.ComposeWatermark — pre-faded in the asset itself since
    /// QuestPDF has no opacity element. Background layer, so it never
    /// displaces content: the text lays out exactly as it would without it.</summary>
    private static void ComposeWatermark(IContainer container)
    {
        if (WatermarkBytes.Length == 0) return;

        container.AlignCenter().AlignMiddle().Width(300).Image(WatermarkBytes).FitWidth();
    }

    // ===== Letterhead =====

    private static void ComposeHeader(IContainer container, ContractPdfModel model)
    {
        container.Row(row =>
        {
            if (LogoBytes.Length > 0)
                row.ConstantItem(140).AlignMiddle().Image(LogoBytes).FitWidth();
            else
                row.ConstantItem(140).AlignMiddle().Text(CompanyName)
                    .FontSize(11).Bold().FontColor(Brand);

            row.RelativeItem();

            row.ConstantItem(170).Border(1).BorderColor(Line).Padding(6).Column(col =>
            {
                col.Item().ContentFromRightToLeft().AlignRight()
                    .Text($"رقم العقد / Contract # : {model.ContractNumber}")
                    .FontFamily(DocumentFonts.Arabic).FontSize(8).FontColor(InkSoft);
                col.Item().ContentFromRightToLeft().AlignRight()
                    .Text($"المراجعة / Revision : {model.RevisionNumber}")
                    .FontFamily(DocumentFonts.Arabic).FontSize(8).FontColor(InkSoft);
                col.Item().ContentFromRightToLeft().AlignRight()
                    .Text($"التاريخ / Date : {model.ApprovedAt:dd/MM/yyyy}")
                    .FontFamily(DocumentFonts.Arabic).FontSize(8).FontColor(InkSoft);
            });
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.PaddingTop(6).Background(FooterBg).PaddingVertical(9).PaddingHorizontal(16).Row(row =>
        {
            row.RelativeItem().Text($"{CompanyPhone1}   {CompanyPhone2}")
                .FontSize(8).Bold().FontColor(White);
            row.RelativeItem().AlignCenter().Text(CompanyEmail).FontSize(8).Bold().FontColor(White);
            row.RelativeItem().AlignRight().Text(CompanyAddress).FontSize(7.5f).FontColor(White);
        });
    }

    // ===== Body =====

    /// <summary>Blank-line-separated paragraphs, each with its own internal
    /// line breaks preserved — a clause heading and the lines under it travel
    /// together as one paragraph, the same grouping <see cref="Generate"/>
    /// expects from <see cref="IContractTextTemplate.BuildDefault"/>.</summary>
    private static List<string> SplitParagraphs(string body) =>
        body.Replace("\r\n", "\n")
            .Split("\n\n")
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

    private static void ComposeParagraph(IContainer container, string paragraph)
    {
        var lines = paragraph.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (lines.Count == 0) return;

        container.Column(column =>
        {
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];

                // Two or more payment lines in a row read as a schedule, so they print as a table.
                if (PaymentRow.IsMatch(line))
                {
                    var run = new List<Match>();
                    var next = index;
                    while (next < lines.Count && PaymentRow.Match(lines[next]) is { Success: true } match)
                    {
                        run.Add(match);
                        next++;
                    }

                    if (run.Count >= 2)
                    {
                        column.Item().PaddingTop(8).Element(x => ComposePaymentTable(x, run));
                        index = next - 1;
                        continue;
                    }
                }

                if (ClauseHeading.IsMatch(line))
                {
                    // A heading carries its own rule underneath, the same way
                    // ComposeSection in BoqPdfGenerator marks a new section —
                    // the one visual cue that says "new clause starts here"
                    // without staff having to add spacing by hand.
                    column.Item().PaddingTop(6).ContentFromRightToLeft().AlignRight()
                        .Text(line).FontFamily(DocumentFonts.Arabic).FontSize(12)
                        .Bold().FontColor(Blue);
                    column.Item().PaddingTop(2).Height(1.5f).Background(Blue);
                    continue;
                }

                if (BulletLine.IsMatch(line))
                {
                    var bulletText = BulletLine.Replace(line, "").Trim();
                    column.Item().PaddingTop(3).ContentFromRightToLeft().Row(row =>
                    {
                        // Bold throughout, not just the headings — Noto Naskh
                        // Arabic reads thin at body size in Regular, and a
                        // legal document's running text needs to be legible
                        // at a glance, not just technically present.
                        row.RelativeItem().AlignRight().Text(bulletText)
                            .FontFamily(DocumentFonts.Arabic).FontSize(9.5f)
                            .Bold().FontColor(Ink).LineHeight(1.5f);
                        row.ConstantItem(12).AlignRight().Text("•")
                            .FontSize(9.5f).FontColor(Accent);
                    });
                    continue;
                }

                column.Item().PaddingTop(3).ContentFromRightToLeft().AlignRight()
                    .Text(line).FontFamily(DocumentFonts.Arabic).FontSize(9.5f)
                    .Bold().FontColor(Ink).LineHeight(1.55f);
            }
        });
    }

    // ===== Payment schedule table =====

    /// <summary>The instalments as a table: payment, percentage, amount, when it falls due — then a total row
    /// that sums the amounts so a reader can see at a glance that the four add up to the contract value.</summary>
    private static void ComposePaymentTable(IContainer container, IReadOnlyList<Match> rows)
    {
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        decimal? amountSum = 0, percentSum = 0;
        foreach (var row in rows)
        {
            if (decimal.TryParse(row.Groups[3].Value.Replace(",", ""), System.Globalization.NumberStyles.Number, invariant, out var amount))
                amountSum += amount;
            else
                amountSum = null;
            if (decimal.TryParse(row.Groups[2].Value.Replace(",", "."), System.Globalization.NumberStyles.Number, invariant, out var percent))
                percentSum += percent;
            else
                percentSum = null;
        }

        // Right-to-left, like the clause around it: the first column (the instalment's name) sits on the right.
        container.ContentFromRightToLeft().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(78);    // الدفعة
                columns.ConstantColumn(46);    // النسبة
                columns.ConstantColumn(96);    // المبلغ
                columns.RelativeColumn(1);     // موعد الاستحقاق
            });

            table.Header(header =>
            {
                PaymentHeader(header.Cell(), "الدفعة");
                PaymentHeader(header.Cell(), "النسبة");
                PaymentHeader(header.Cell(), "المبلغ (ريال قطري)");
                PaymentHeader(header.Cell(), "موعد الاستحقاق");
            });

            foreach (var row in rows)
            {
                PaymentCell(table.Cell()).AlignRight().Text(row.Groups[1].Value).Bold();
                PaymentCell(table.Cell()).AlignCenter().Text($"{row.Groups[2].Value}%").Bold();
                PaymentCell(table.Cell()).AlignCenter().Text(row.Groups[3].Value).Bold();
                PaymentCell(table.Cell()).AlignRight().Text(row.Groups[4].Value.Trim()).LineHeight(1.5f);
            }

            if (amountSum is { } total && percentSum is { } percentTotal)
            {
                table.Cell().Background(SectionBg).BorderTop(1).BorderColor(Blue).PaddingVertical(6).PaddingHorizontal(6)
                    .AlignRight().Text("الإجمالي").Bold().FontColor(Blue);
                table.Cell().Background(SectionBg).BorderTop(1).BorderColor(Blue).PaddingVertical(6).PaddingHorizontal(6)
                    .AlignCenter().Text($"{percentTotal.ToString("0.##", invariant)}%").Bold().FontColor(Blue);
                table.Cell().Background(SectionBg).BorderTop(1).BorderColor(Blue).PaddingVertical(6).PaddingHorizontal(6)
                    .AlignCenter().Text(total.ToString("N2", invariant)).Bold().FontColor(Blue);
                table.Cell().Background(SectionBg).BorderTop(1).BorderColor(Blue).PaddingVertical(6).PaddingHorizontal(6)
                    .Text(string.Empty);
            }
        });
    }

    private static void PaymentHeader(IContainer cell, string text) =>
        cell.Background(Blue).PaddingVertical(6).PaddingHorizontal(6).AlignCenter()
            .Text(text).FontFamily(DocumentFonts.Arabic).FontSize(9.5f).Bold().FontColor(White);

    private static IContainer PaymentCell(IContainer cell) =>
        cell.BorderBottom(0.75f).BorderColor(Line).PaddingVertical(6).PaddingHorizontal(6)
            .DefaultTextStyle(x => x.FontFamily(DocumentFonts.Arabic).FontSize(9.5f).FontColor(Ink));

    // ===== Price schedule =====

    private static string Money(decimal value) => value.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);

    private static string Qty(decimal value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The quotation's amounts, printed inside the contract: a table per section, then the totals.</summary>
    private static void ComposePrices(IContainer container, ContractPriceSchedule prices)
    {
        container.Column(column =>
        {
            column.Item().Column(heading =>
            {
                heading.Item().AlignCenter().Text("جدول الكميات والأسعار")
                    .FontFamily(DocumentFonts.Arabic).FontSize(17).ExtraBold().FontColor(Blue);
                heading.Item().AlignCenter().Text("Bill of quantities & prices")
                    .FontSize(10).Bold().FontColor(Ink);
                heading.Item().PaddingTop(6).Height(3).Background(Blue);
            });

            foreach (var section in prices.Sections)
                column.Item().PaddingTop(14).Element(x => ComposePriceSection(x, section));

            column.Item().PaddingTop(16).Element(x => ComposePriceTotals(x, prices));
        });
    }

    private static void ComposePriceSection(IContainer container, BoqPdfSection section)
    {
        container.Column(column =>
        {
            column.Item().Background(SectionBg).PaddingVertical(6).PaddingHorizontal(8)
                .Text($"{section.Number}.  {section.Title}").FontSize(10.5f).Bold().FontColor(Blue);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(32);    // no.
                    columns.RelativeColumn(1);     // description
                    columns.ConstantColumn(36);    // unit
                    columns.ConstantColumn(36);    // qty
                    columns.ConstantColumn(66);    // unit price
                    columns.ConstantColumn(76);    // total
                });

                table.Header(header =>
                {
                    PriceHeader(header.Cell(), "م", "No.");
                    PriceHeader(header.Cell(), "البيان", "Description");
                    PriceHeader(header.Cell(), "الوحدة", "Unit");
                    PriceHeader(header.Cell(), "الكمية", "Qty");
                    PriceHeader(header.Cell(), "سعر الوحدة", "Unit price");
                    PriceHeader(header.Cell(), "الإجمالي", "Total");
                });

                foreach (var line in section.Lines)
                {
                    PriceCell(table.Cell()).Text(line.Number).FontSize(9);
                    PriceCell(table.Cell()).Column(cell =>
                    {
                        cell.Item().Text(line.ItemName).FontSize(9.5f).Bold();
                        var detail = string.Join("  ·  ", new[] { line.Brand, line.Type, line.ModelNo }
                            .Where(v => !string.IsNullOrWhiteSpace(v)));
                        if (detail.Length > 0)
                            cell.Item().PaddingTop(1).Text(detail).FontSize(8);
                    });
                    PriceCell(table.Cell()).AlignCenter().Text(line.Uom).FontSize(9);
                    PriceCell(table.Cell()).AlignCenter().Text(Qty(line.Quantity)).FontSize(9);
                    PriceCell(table.Cell()).AlignRight().Text(Money(line.UnitRate)).FontSize(9);
                    PriceCell(table.Cell()).AlignRight().Text(Money(line.LineTotal)).FontSize(9.5f).Bold();
                }

                // The section's own total, under its column of line totals.
                table.Cell().ColumnSpan(5).BorderTop(1).BorderColor(Blue).PaddingVertical(5).PaddingHorizontal(5)
                    .AlignRight().Text("Section total / إجمالي القسم").FontSize(9).Bold().FontColor(Blue);
                table.Cell().BorderTop(1).BorderColor(Blue).PaddingVertical(5).PaddingHorizontal(5)
                    .AlignRight().Text(Money(section.Subtotal)).FontSize(9.5f).Bold().FontColor(Blue);
            });
        });
    }

    /// <summary>A blue header cell, Arabic over English, white text.</summary>
    private static void PriceHeader(IContainer cell, string arabic, string english)
    {
        cell.Background(Blue).PaddingVertical(5).PaddingHorizontal(4).Column(inner =>
        {
            inner.Item().AlignCenter().Text(arabic).FontFamily(DocumentFonts.Arabic)
                .FontSize(8.5f).Bold().FontColor(White);
            inner.Item().AlignCenter().Text(english).FontSize(7.5f).Bold().FontColor(White);
        });
    }

    /// <summary>A body cell: black text, a hairline under it, even padding.</summary>
    private static IContainer PriceCell(IContainer cell) =>
        cell.BorderBottom(0.75f).BorderColor(Line).PaddingVertical(5).PaddingHorizontal(5).DefaultTextStyle(x => x.FontColor(Ink));

    private static void ComposePriceTotals(IContainer container, ContractPriceSchedule prices)
    {
        container.AlignRight().Width(250).Column(column =>
        {
            TotalRow(column, "الإجمالي / Total", Money(prices.Total), false);
            if (prices.SpecialDiscount > 0)
                TotalRow(column, "الخصم / Discount", "-" + Money(prices.SpecialDiscount), false);

            column.Item().PaddingTop(4).Background(Blue).PaddingVertical(8).PaddingHorizontal(10).Row(row =>
            {
                row.RelativeItem().Text("المبلغ المستحق / Amount payable (QAR)").FontSize(9.5f).Bold().FontColor(White);
                row.AutoItem().Text(Money(prices.GrandTotal)).FontSize(12).ExtraBold().FontColor(White);
            });
        });
    }

    private static void TotalRow(ColumnDescriptor column, string label, string value, bool strong)
    {
        column.Item().BorderBottom(0.75f).BorderColor(Line).PaddingVertical(5).PaddingHorizontal(10).Row(row =>
        {
            row.RelativeItem().Text(label).FontSize(9.5f).FontColor(Ink);
            row.AutoItem().Text(value).FontSize(9.5f).Bold().FontColor(Ink);
        });
    }

    // ===== Signatures =====

    private static void ComposeSignatures(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text("توقيعات الأطراف")
                .FontFamily(DocumentFonts.Arabic).FontSize(12).Bold().FontColor(Blue);

            // The second party sits left and the first sits right — RTL
            // convention places whichever is named first (الطرف الأول) on
            // the right, the same side the body text itself reads from.
            col.Item().PaddingTop(30).Row(row =>
            {
                row.RelativeItem().Column(inner =>
                {
                    inner.Item().AlignCenter().Text("الطرف الثاني (مالك العقار)")
                        .FontFamily(DocumentFonts.Arabic).FontSize(9.5f).Bold().FontColor(InkSoft);
                    inner.Item().PaddingTop(45).AlignCenter().Width(160).Height(1).Background(Ink);
                });

                row.RelativeItem().Column(inner =>
                {
                    inner.Item().AlignCenter().Text("الطرف الأول (المقاول)")
                        .FontFamily(DocumentFonts.Arabic).FontSize(9.5f).Bold().FontColor(InkSoft);
                    inner.Item().AlignCenter().Text(CompanyNameAr)
                        .FontFamily(DocumentFonts.Arabic).FontSize(9).FontColor(InkSoft);
                    inner.Item().PaddingTop(45).AlignCenter().Width(160).Height(1).Background(Ink);
                });
            });
        });
    }
}
