using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Proformas;
using static Jama.Infrastructure.Documents.WordKit;

namespace Jama.Infrastructure.Documents;

/// <summary>
/// The proforma invoice as a Word document, laid out like the PDF: logo and boxed title, the customer box beside the
/// references, one table grouped by section (headings repeat on each page), totals, notes, terms, payment details and
/// the company at the end. A4, page numbers in the footer. Everything stays editable.
/// </summary>
public sealed class ProformaWordGenerator : IProformaWordGenerator
{
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
            var pictures = new Pictures(main);

            body.Append(Header(pictures, m));
            if (m.Draft) body.Append(P(JustificationValues.Center, R("DRAFT — NOT APPROVED", bold: true, color: DocumentLook.DraftRed, size: 22)));
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

            body.Append(Section(footerId));
            main.Document.Save();
        }
        return stream.ToArray();
    }

    // ---------- parts ----------

    private static Table Header(Pictures pictures, ProformaModel m)
    {
        var logo = DocumentLook.Logo.Length > 0
            ? P(JustificationValues.Left, new Run(pictures.Png(DocumentLook.Logo, 1_750_000L, "Jama Go logo")))
            : P(JustificationValues.Left, R("JAMA GO", bold: true, color: DocumentLook.Brand, size: 40));

        var title = Cell(4200, [P(JustificationValues.Center, R("PROFORMA INVOICE", bold: true, color: DocumentLook.BrandStrong, size: 30))],
            borderColor: DocumentLook.BrandStrong, borderSize: 12);

        return Grid([ContentWidth - 4200, 4200],
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
            ("Date#", DocumentLook.Date(m.Date)),
            ("PI No", m.Number ?? (m.Draft ? "DRAFT" : "Given on download")),
            ("Quote No#", m.QuoteNumber),
            ("Quote Date#", DocumentLook.Date(m.QuoteDate)),
            ("Ref#", m.Ref),
        ];

        var rows = new List<TableRow>();
        for (var i = 0; i < 5; i++)
        {
            var left = i == 0
                ? Cell(customer, [P(null, R("Customer Details:", bold: true))], fill: DocumentLook.HeadBg)
                : Cell(customer, [P(null, R($"{customerLines[i - 1].Label} : ", bold: true, color: DocumentLook.Muted),
                    R(DocumentLook.Dash(customerLines[i - 1].Value), bold: true))]);
            rows.Add(new TableRow(
                left,
                Cell(gap, [P(null)], noBorders: true),
                Cell(label, [P(null, R(references[i].Label, bold: true))], fill: DocumentLook.HeadBg),
                Cell(value, [P(JustificationValues.Right, R(DocumentLook.Dash(references[i].Value), bold: true))])));
        }
        return Grid([customer, gap, label, value], [.. rows]);
    }

    private static Table Items(ProformaModel m)
    {
        int[] widths = [560, 3900, 1000, 1700, 600, 500, 1000, ContentWidth - 9260];
        string[] headings = ["SNO", "DESCRIPTION", "BRAND", "MODEL / PART NO.", "UNITS", "QTY", "UNIT PRICE", "TOTAL AMOUNT"];

        var header = new TableRow(new TableRowProperties(new TableHeader(), new CantSplit()));
        for (var c = 0; c < widths.Length; c++)
            header.Append(Cell(widths[c], [P(JustificationValues.Center, R(headings[c], bold: true, size: 14))], fill: DocumentLook.HeadBg));

        var rows = new List<TableRow> { header };
        foreach (var section in m.Sections)
        {
            if (!string.IsNullOrWhiteSpace(section.Title))
                rows.Add(new TableRow(new TableRowProperties(new CantSplit()),
                    Cell(ContentWidth, [P(JustificationValues.Center, R(section.Title.Trim().ToUpperInvariant(), bold: true, color: "000000", size: 17))],
                        fill: DocumentLook.SectionBg, span: widths.Length)));

            foreach (var line in section.Lines)
            {
                rows.Add(new TableRow(new TableRowProperties(new CantSplit()),
                    Cell(widths[0], [P(JustificationValues.Center, R(line.Number.ToString(), size: SmallSize))]),
                    Cell(widths[1], [P(JustificationValues.Left, R(line.Description, size: SmallSize))]),
                    Cell(widths[2], [P(JustificationValues.Center, R(line.Brand ?? string.Empty, size: SmallSize))]),
                    Cell(widths[3], [P(JustificationValues.Center, R(line.Model ?? string.Empty, size: SmallSize))]),
                    Cell(widths[4], [P(JustificationValues.Center, R(ProformaText.Unit(line.Unit), size: SmallSize))]),
                    Cell(widths[5], [P(JustificationValues.Center, R(DocumentLook.Qty(line.Quantity), size: SmallSize))]),
                    Cell(widths[6], [P(JustificationValues.Right, R(DocumentLook.Money(line.UnitPrice), size: SmallSize))]),
                    Cell(widths[7], [P(JustificationValues.Right, R(DocumentLook.Money(line.Total), size: SmallSize))])));
            }
        }

        if (rows.Count == 1)
            rows.Add(new TableRow(Cell(ContentWidth, [P(JustificationValues.Center, R("This quotation has no items.", color: DocumentLook.Muted))],
                span: widths.Length)));

        return Grid(widths, [.. rows]);
    }

    private static Table Totals(ProformaModel m)
    {
        const int value = 1800, label = 2200, left = ContentWidth - value - label;

        var notes = new List<OpenXmlElement>();
        var po = DocumentLook.PoNote(m.PoNumber, m.PoDate);
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
            var size = totals[i].Strong ? 20 : BodySize;
            var fill = totals[i].Strong ? DocumentLook.HeadBg : null;
            rows.Add(new TableRow(
                leftCell,
                Cell(label, [P(JustificationValues.Right, R(totals[i].Label, bold: true, size: size))], fill: fill),
                Cell(value, [P(JustificationValues.Right, R(DocumentLook.Money(totals[i].Value), bold: true, size: size))], fill: fill)));
        }
        return Grid([left, label, value], [.. rows]);
    }

    private static Table TermsAndPayment()
    {
        const int right = 4800, left = ContentWidth - right;

        var terms = new List<OpenXmlElement> { P(null, R("Terms & Conditions:", bold: true, color: DocumentLook.BrandStrong, size: 20)) };
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
        var payTable = Grid([payLabel, payValue], [.. payment.Select(p => new TableRow(
            Cell(payLabel, [P(null, R(p.Label, bold: true))], fill: DocumentLook.HeadBg),
            Cell(payValue, [P(null, R(p.Value, bold: true))])))]);

        var pay = new List<OpenXmlElement>
        {
            P(null, R("Payment details as follows:", bold: true, color: DocumentLook.BrandStrong, size: 20)),
            payTable,
            P(null),   // a table cell must end with a paragraph
        };

        return Grid([left, right],
            new TableRow(Cell(left, [.. terms], noBorders: true), Cell(right, [.. pay], noBorders: true)));
    }

    private static IEnumerable<Paragraph> Company()
    {
        yield return Par(new Para(JustificationValues.Center,
                Borders: new ParagraphBorders(new TopBorder { Val = BorderValues.Single, Size = 12, Color = DocumentLook.Brand, Space = 4 })),
            R(ProformaText.CompanyName, bold: true, color: DocumentLook.BrandStrong, size: 22));
        yield return P(JustificationValues.Center, R(ProformaText.CompanyAddress, color: DocumentLook.Muted, size: SmallSize));
        yield return P(JustificationValues.Center, R($"Mobile {ProformaText.CompanyMobiles}", color: DocumentLook.Muted, size: SmallSize));
    }
}
