using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using static Jama.Infrastructure.Documents.WordKit;
using Q = Jama.Infrastructure.Documents.BoqPdfGenerator;

namespace Jama.Infrastructure.Documents;

/// <summary>
/// The whole quotation as an editable Word document, in the PDF's order and wording: the cover (ref, COMMERCIAL
/// PROPOSAL, prepared for / project / date, the company), the commercial-offer letter with its terms in the chosen
/// language(s) and the signature, then the priced items section by section — with the product photos and
/// descriptions — the totals and the amount in words. The letter follows the same "include terms page" choice as the
/// PDF. Page numbers in the footer; the item headings repeat on every page.
/// </summary>
public sealed partial class BoqWordGenerator(IBoqPdfGenerator pdf) : IBoqWordGenerator
{
    private const long LogoWidth = 1_550_000L;   // EMU, about 1.7 inches
    private const long QrWidth = 560_000L;
    private const long PhotoBox = 430_000L;

    [GeneratedRegex(@"^\d+\.\s+\S")]
    private static partial Regex NumberedHeading();

    public byte[] Generate(BoqPdfModel m)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            doc.PackageProperties.Title = $"Quotation {Q.NumberWithRevision(m)}";
            doc.PackageProperties.Creator = Q.CompanyName;

            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body());
            AddStyles(main);
            var footerId = AddFooter(main, $"{Q.CompanyName} · Doha, Qatar · {Q.NumberWithRevision(m)}");
            var body = main.Document.Body!;
            var pictures = new Pictures(main);

            foreach (var element in Cover(pictures, m)) body.Append(element);
            body.Append(PageBreak());

            if (m.IncludeTermsPage)
            {
                foreach (var element in Letter(pictures, m)) body.Append(element);
                body.Append(PageBreak());
            }

            foreach (var element in Items(pictures, m)) body.Append(element);

            body.Append(Section(footerId));
            main.Document.Save();
        }
        return stream.ToArray();
    }

    // ---------- page 1: cover ----------

    private IEnumerable<OpenXmlElement> Cover(Pictures pictures, BoqPdfModel m)
    {
        yield return Corners(pictures);
        yield return Spacer(900);
        yield return P(JustificationValues.Center, R("Ref No: ", color: DocumentLook.InkSoft, size: 22), R(Q.NumberWithRevision(m), bold: true, size: 22));
        if (m.Watermark) yield return Draft();
        yield return Spacer(420);

        const int titleWidth = 4600;
        yield return Grid([titleWidth], JustificationValues.Center,
            Row(false, Cell(titleWidth, [Par(new Para(JustificationValues.Center, Before: 120, After: 120), R("COMMERCIAL PROPOSAL", bold: true, size: 28))],
                borderColor: DocumentLook.Ink, borderSize: 12)));
        yield return Spacer(420);

        const int panelWidth = 6600;
        OpenXmlElement[] panel =
        [
            Par(new Para(JustificationValues.Center, Before: 160), R("Prepared For:", color: DocumentLook.InkSoft, size: 19)),
            Par(new Para(JustificationValues.Center, Before: 60), R(Q.CoverClient(m), bold: true, size: 23)),
            Rule(),
            P(JustificationValues.Center, R("Project Title:", color: DocumentLook.InkSoft, size: 19)),
            Par(new Para(JustificationValues.Center, Before: 60), R(m.ProjectName.ToUpperInvariant(), bold: true, size: 23)),
            Rule(),
            Par(new Para(JustificationValues.Center, After: 160),
                R("Date of Submission: ", color: DocumentLook.InkSoft, size: 19), R(Q.LongDate(m.IssueDate), bold: true, size: 19)),
        ];
        yield return Grid([panelWidth], JustificationValues.Center,
            Row(false, Cell(panelWidth, panel, fill: DocumentLook.PanelBg, noBorders: true)));

        yield return Spacer(900);
        yield return P(null, R("Prepared By:", bold: true, italic: true, underline: true, color: DocumentLook.Accent, size: 20));
        yield return Spacer(200);
        yield return Company(pictures);
    }

    /// <summary>The logo top left and the QR code top right, as the PDF's cover and letter open.</summary>
    private static Table Corners(Pictures pictures)
    {
        const int logo = 3200, qr = 1000;
        var logoCell = DocumentLook.Logo.Length > 0
            ? P(JustificationValues.Left, new Run(pictures.Png(DocumentLook.Logo, LogoWidth, "Jama Go logo")))
            : P(JustificationValues.Left, R(Q.CompanyName, bold: true, color: DocumentLook.Brand, size: 24));
        var qrCell = DocumentLook.Qr.Length > 0
            ? P(JustificationValues.Right, new Run(pictures.Png(DocumentLook.Qr, QrWidth, "jamago.qa QR code")))
            : P(null);
        return Grid([logo, ContentWidth - logo - qr, qr],
            Row(false,
                Cell(logo, [logoCell], noBorders: true),
                Cell(ContentWidth - logo - qr, [P(null)], noBorders: true),
                Cell(qr, [qrCell], noBorders: true, valign: TableVerticalAlignmentValues.Top)));
    }

    /// <summary>The logo beside the letterhead lines, as the cover sets them.</summary>
    private static Table Company(Pictures pictures)
    {
        const int logo = 2400;
        OpenXmlElement[] details =
        [
            P(null, R(Q.CompanyName, bold: true, color: DocumentLook.BrandStrong, size: 24)),
            P(null, R(Q.CompanyTagline, italic: true, color: DocumentLook.Accent, size: 17)),
            Par(new Para(Before: 120), R(Q.CompanyAddress, color: DocumentLook.InkSoft, size: 17)),
            P(null, R(Q.CompanyCity, color: DocumentLook.InkSoft, size: 17)),
            Par(new Para(Before: 100), R("Telephone :  ", color: DocumentLook.InkSoft, size: 17), R(Q.CompanyPhone1, size: 17)),
            P(null, R("                   ", size: 17), R(Q.CompanyPhone2, size: 17)),
            P(null, R("                   ", size: 17), R(Q.CompanyPhone3, size: 17)),
            P(null, R("Email :  ", color: DocumentLook.InkSoft, size: 17), R(Q.CompanyEmail, bold: true, underline: true, color: DocumentLook.Brand, size: 17)),
            P(null, R("Website :  ", color: DocumentLook.InkSoft, size: 17), R(Q.CompanyWebsite, bold: true, underline: true, color: DocumentLook.Brand, size: 17)),
        ];
        var logoCell = DocumentLook.Logo.Length > 0
            ? P(null, new Run(pictures.Png(DocumentLook.Logo, 1_250_000L, "Jama Go logo")))
            : P(null);
        return Grid([logo, ContentWidth - logo],
            Row(false,
                Cell(logo, [logoCell], noBorders: true, valign: TableVerticalAlignmentValues.Top),
                Cell(ContentWidth - logo, details, noBorders: true, valign: TableVerticalAlignmentValues.Top)));
    }

    // ---------- page 2: the commercial offer ----------

    private IEnumerable<OpenXmlElement> Letter(Pictures pictures, BoqPdfModel m)
    {
        yield return Corners(pictures);
        yield return Par(new Para(Before: 100, Borders: BottomRule(DocumentLook.Brand, 12)),
            R("Ref No: ", color: DocumentLook.InkSoft), R(Q.NumberWithRevision(m), bold: true));
        if (m.Watermark) yield return Draft();
        yield return Spacer(200);

        const int date = 2600;
        yield return Grid([ContentWidth - date, date],
            Row(false,
                Cell(ContentWidth - date,
                    [P(null, R(Q.CoverClient(m), bold: true, size: 21)), P(null, R("Procurement Department", color: DocumentLook.InkSoft))],
                    noBorders: true, valign: TableVerticalAlignmentValues.Top),
                Cell(date, [P(JustificationValues.Right, R(Q.LongDate(m.IssueDate), color: DocumentLook.InkSoft))],
                    noBorders: true, valign: TableVerticalAlignmentValues.Top)));
        yield return Spacer(140);

        const int label = 1700, colon = 200;
        yield return Grid([label, colon, ContentWidth - label - colon],
            Subject("Project Title", m.ProjectName, label, colon),
            Subject("Subject", "Priced / Commercial Offer", label, colon));

        yield return Par(new Para(Before: 160), R("Dear Sir / Madam,", bold: true, size: 19));
        yield return Par(new Para(Before: 80), R(
            "With reference to the above subject, and to the requirements stated in your request, we are pleased to " +
            "submit our priced commercial offer with the scope of work set out below.", color: DocumentLook.InkSoft));

        if (m.TermsLanguage != BoqTermsLanguage.Arabic)
            foreach (var p in Terms(string.IsNullOrWhiteSpace(m.TermsOverride) ? pdf.DefaultTermsText() : m.TermsOverride!, arabic: false))
                yield return p;

        if (m.TermsLanguage == BoqTermsLanguage.Both)
            yield return Par(new Para(Before: 240, RightToLeft: true, Borders: BottomRule(DocumentLook.Border, 4)),
                R("الشروط والأحكام", bold: true, color: DocumentLook.Brand, size: 21, rightToLeft: true));

        if (m.TermsLanguage != BoqTermsLanguage.English)
            foreach (var p in Terms(string.IsNullOrWhiteSpace(m.TermsOverrideAr) ? pdf.DefaultTermsTextArabic() : m.TermsOverrideAr!, arabic: true))
                yield return p;

        yield return Par(new Para(Before: 200), R("Yours faithfully,", color: DocumentLook.InkSoft));
        var signedBy = Q.PreparedBy(m.PreparedByName);
        if (signedBy is not null)
        {
            var rows = new List<TableRow> { Signature("Name", signedBy, strong: true) };
            if (Q.PreparedByEmail(m.PreparedByName) is { } email) rows.Add(Signature("Email", email));
            if (!string.IsNullOrWhiteSpace(m.PreparedByDesignation)) rows.Add(Signature("Designation", m.PreparedByDesignation!));
            yield return Grid([1300, 5000], [.. rows]);
        }
        yield return Par(new Para(Before: 60), R(Q.CompanyName, bold: signedBy is null, color: signedBy is null ? DocumentLook.Ink : DocumentLook.Muted, size: 17));
    }

    private static TableRow Subject(string label, string value, int labelWidth, int colonWidth) =>
        Row(false,
            Cell(labelWidth, [P(null, R(label, color: DocumentLook.Muted))], noBorders: true),
            Cell(colonWidth, [P(null, R(":", color: DocumentLook.Muted))], noBorders: true),
            Cell(ContentWidth - labelWidth - colonWidth, [P(null, R(value, bold: true))], noBorders: true));

    private static TableRow Signature(string label, string value, bool strong = false) =>
        Row(false,
            Cell(1300, [P(null, R(label + ":", color: DocumentLook.Muted))], noBorders: true),
            Cell(5000, [P(null, R(value, bold: strong, size: 19))], noBorders: true));

    /// <summary>The terms as the PDF lays out typed terms: "1. Heading" lines bold and blue, "- " lines as bullets,
    /// anything else as plain text. Arabic runs right to left.</summary>
    private static IEnumerable<Paragraph> Terms(string text, bool arabic)
    {
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            if (NumberedHeading().IsMatch(line))
                yield return Par(new Para(Before: 120, KeepNext: true, RightToLeft: arabic),
                    R(line, bold: true, color: DocumentLook.BrandStrong, size: 19, rightToLeft: arabic));
            else if (line.StartsWith("- ", StringComparison.Ordinal))
                yield return Par(new Para(Before: 30, RightToLeft: arabic),
                    R("      •  ", color: DocumentLook.Accent, size: 17, rightToLeft: arabic),
                    R(line[2..].Trim(), color: DocumentLook.InkSoft, size: 17, rightToLeft: arabic));
            else
                yield return Par(new Para(Before: 30, RightToLeft: arabic),
                    R(line, color: DocumentLook.InkSoft, size: 17, rightToLeft: arabic));
        }
    }

    // ---------- page 3: the priced items ----------

    private static IEnumerable<OpenXmlElement> Items(Pictures pictures, BoqPdfModel m)
    {
        const int titleWidth = 4200;
        var logoCell = DocumentLook.Logo.Length > 0
            ? P(null, new Run(pictures.Png(DocumentLook.Logo, LogoWidth, "Jama Go logo")))
            : P(null, R(Q.CompanyName, bold: true, color: DocumentLook.Brand, size: 24));
        yield return Grid([ContentWidth - titleWidth, titleWidth],
            Row(false,
                Cell(ContentWidth - titleWidth, [logoCell], noBorders: true),
                Cell(titleWidth,
                    [
                        P(JustificationValues.Right, R("QUOTATION", bold: true, color: DocumentLook.BrandStrong, size: 44)),
                        P(JustificationValues.Right, R("Quotation no  ", color: DocumentLook.Muted), R(Q.NumberWithRevision(m), bold: true, size: 22)),
                    ],
                    noBorders: true)));
        yield return Par(new Para(Borders: BottomRule(DocumentLook.Brand, 18)));
        if (m.Watermark) yield return Draft();
        yield return Spacer(200);

        yield return Details(m);

        int[] widths = [500, 1000, 1000, 850, 1250, ContentWidth - 7900, 650, 500, 1000, 1150];
        string[] headings = ["S.No", "Brand", "Image", "Type", "Model", "Description", "Unit", "Qty", "Unit Price", "Total Amount"];

        foreach (var section in m.Sections)
        {
            yield return Spacer(240);
            var rows = new List<TableRow>
            {
                Row(true, Cell(ContentWidth,
                    [P(JustificationValues.Center, R($"{section.Number}.  {section.Title}", bold: true, color: DocumentLook.BrandStrong, size: 20))],
                    fill: DocumentLook.SectionBg, span: widths.Length)),
            };
            var header = Row(true);
            for (var c = 0; c < widths.Length; c++)
                header.Append(Cell(widths[c],
                    [P(c >= 6 ? JustificationValues.Right : JustificationValues.Left, R(headings[c], bold: true, color: "FFFFFF", size: 14))],
                    fill: DocumentLook.BrandStrong));
            rows.Add(header);

            foreach (var line in section.Lines)
            {
                var description = new List<OpenXmlElement> { P(null, R(line.ItemName, bold: true, size: SmallSize)) };
                if (!string.IsNullOrWhiteSpace(line.Description))
                    description.Add(Par(new Para(Before: 20), R(line.Description!.Trim(), color: DocumentLook.Muted, size: 15)));
                if (!string.IsNullOrWhiteSpace(line.DescriptionAr))
                    description.Add(Par(new Para(Before: 20, RightToLeft: true), R(line.DescriptionAr!.Trim(), color: DocumentLook.Muted, size: 15, rightToLeft: true)));

                var photo = line.Image is { Length: > 0 } bytes ? pictures.Photo(bytes, PhotoBox, $"Item {line.Number}") : null;
                rows.Add(Row(false,
                    Cell(widths[0], [P(null, R(line.Number, color: DocumentLook.Muted, size: SmallSize))]),
                    Cell(widths[1], [P(null, R(Q.Dash(line.Brand), color: DocumentLook.InkSoft, size: SmallSize))]),
                    Cell(widths[2], [photo is null ? P(JustificationValues.Center, R("—", color: DocumentLook.Muted)) : P(JustificationValues.Center, new Run(photo))]),
                    Cell(widths[3], [P(null, R(Q.Dash(line.Type), color: DocumentLook.InkSoft, size: 15))]),
                    Cell(widths[4], [P(null, R(Q.Dash(line.ModelNo), color: DocumentLook.InkSoft, size: 15))]),
                    Cell(widths[5], [.. description]),
                    Cell(widths[6], [P(JustificationValues.Right, R(Q.Unit(line.Uom), color: DocumentLook.InkSoft, size: SmallSize))]),
                    Cell(widths[7], [P(JustificationValues.Right, R(Q.Qty(line.Quantity), size: SmallSize))]),
                    Cell(widths[8], [P(JustificationValues.Right, R(Q.Money(line.UnitRate), size: SmallSize))]),
                    Cell(widths[9], [P(JustificationValues.Right, R(Q.Money(line.LineTotal), bold: true, size: SmallSize))])));
            }

            rows.Add(Row(false,
                Cell(ContentWidth - widths[9], [P(JustificationValues.Right, R("Section total", bold: true, color: DocumentLook.Muted, size: 15))],
                    span: widths.Length - 1),
                Cell(widths[9], [P(JustificationValues.Right, R(Q.Money(section.Subtotal), bold: true, color: DocumentLook.BrandStrong))])));

            yield return Grid(widths, [.. rows]);
        }

        yield return Spacer(280);
        yield return Totals(m);
        yield return Spacer(160);
        yield return Par(new Para(), R("AMOUNT IN WORDS  ", bold: true, color: DocumentLook.Muted, size: 14), R(MoneyWords.English(m.GrandTotal), bold: true));
        yield return Par(new Para(Before: 60, RightToLeft: true),
            R("المبلغ كتابةً  ", bold: true, color: DocumentLook.Muted, size: 14, rightToLeft: true),
            R(MoneyWords.Arabic(m.GrandTotal), bold: true, rightToLeft: true));

        if (!string.IsNullOrWhiteSpace(m.Notes))
        {
            yield return Par(new Para(Before: 240), R("NOTES", bold: true, color: DocumentLook.Muted, size: 14));
            foreach (var line in m.Notes!.Replace("\r\n", "\n").Split('\n'))
                yield return P(null, R(line, color: DocumentLook.InkSoft));
        }
    }

    private static Table Details(BoqPdfModel m)
    {
        const int label = 1500;
        var value = (ContentWidth - 2 * label) / 2;

        TableRow Pair(string leftLabel, string? leftValue, string rightLabel, string? rightValue) =>
            Row(false, Label(leftLabel), Value(value, leftValue), Label(rightLabel), Value(ContentWidth - 2 * label - value, rightValue));
        TableCell Label(string text) =>
            Cell(label, [P(null, R(text.ToUpperInvariant(), bold: true, color: DocumentLook.Muted, size: 13))], fill: DocumentLook.PanelBg);
        TableCell Value(int width, string? text) =>
            Cell(width, [P(null, R(Q.Dash(text), bold: true))]);

        return Grid([label, value, label, ContentWidth - 2 * label - value],
            Pair("To", m.ClientName, "Date", Q.Date(m.IssueDate)),
            Pair("Project", m.ProjectName, "Location", m.SiteLocation),
            Pair("Contact number", m.ContactNumber, "Prepared by", Q.PreparedBy(m.PreparedByName)),
            Row(false, Label("Valid until"),
                Cell(ContentWidth - label,
                    [P(null,
                        R(Q.Date(m.IssueDate.AddDays(Q.ValidityDays)), bold: true, color: DocumentLook.Accent),
                        R($"     Valid for {Q.ValidityDays} days (2 weeks) from the date above.", color: DocumentLook.Muted, size: SmallSize))],
                    span: 3)));
    }

    private static Table Totals(BoqPdfModel m)
    {
        const int label = 3200, value = 1800;
        var discounted = m.SpecialDiscount > 0;

        TableRow Line(string english, string arabic, string amount, bool strong, string? amountColor = null)
        {
            var fill = strong ? DocumentLook.BrandStrong : null;
            var color = strong ? "FFFFFF" : null;
            return Row(false,
                Cell(label,
                    [
                        P(JustificationValues.Right, R(english, bold: true, color: color, size: strong ? 20 : BodySize)),
                        Par(new Para(RightToLeft: true), R(arabic, color: strong ? "FFFFFF" : DocumentLook.Muted, size: 15, rightToLeft: true)),
                    ], fill: fill),
                Cell(value, [P(JustificationValues.Right, R(amount, bold: true, color: color ?? amountColor, size: strong ? 22 : BodySize))], fill: fill));
        }

        var rows = new List<TableRow> { Line("Total", "الإجمالي", Q.Money(m.Total), false) };
        if (discounted) rows.Add(Line("Discount", "الخصم", Q.Minus(m.SpecialDiscount), false, DocumentLook.Accent));
        rows.Add(Line(discounted ? "FINAL AMOUNT (QAR)" : "TOTAL (QAR)", discounted ? "المبلغ النهائي (ر.ق)" : "الإجمالي (ر.ق)",
            Q.Money(m.GrandTotal), true));
        return Grid([label, value], JustificationValues.Right, [.. rows]);
    }

    // ---------- small pieces ----------

    private static Paragraph Draft() =>
        Par(new Para(JustificationValues.Center, Before: 80), R("DRAFT — NOT APPROVED", bold: true, color: DocumentLook.DraftRed, size: 22));

    private static Paragraph Rule() => Par(new Para(Borders: BottomRule(DocumentLook.Accent, 6), After: 160));

    private static ParagraphBorders BottomRule(string color, uint size) =>
        new(new BottomBorder { Val = BorderValues.Single, Size = size, Color = color, Space = 4 });
}
