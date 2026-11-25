using ClosedXML.Excel;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Proformas;

namespace Jama.Infrastructure.Documents;

/// <summary>
/// The proforma invoice as an Excel workbook, laid out like the PDF (without the logo): the boxed title, the customer box beside
/// the references, one table grouped by section, totals, notes, terms, payment details and the company at the foot.
/// Values, not formulas, so every viewer (phones included) shows the same figures the PDF does. Prints on A4, one page
/// wide, with the table headings repeated on every page.
/// </summary>
public sealed class ProformaExcelGenerator : IProformaExcelGenerator
{
    private static readonly XLColor Brand = XLColor.FromHtml("#" + ProformaLook.Brand);
    private static readonly XLColor BrandStrong = XLColor.FromHtml("#" + ProformaLook.BrandStrong);
    private static readonly XLColor Ink = XLColor.FromHtml("#" + ProformaLook.Ink);
    private static readonly XLColor Muted = XLColor.FromHtml("#" + ProformaLook.Muted);
    private static readonly XLColor Border = XLColor.FromHtml("#" + ProformaLook.Border);
    private static readonly XLColor HeadBg = XLColor.FromHtml("#" + ProformaLook.HeadBg);
    private static readonly XLColor SectionBg = XLColor.FromHtml("#" + ProformaLook.SectionBg);
    private static readonly XLColor DraftRed = XLColor.FromHtml("#" + ProformaLook.DraftRed);

    private const int Columns = 8;
    private static readonly double[] Widths = [6, 46, 13, 22, 8, 7, 13, 15];
    private const string MoneyFormat = "#,##0.00";
    private const double LineHeight = 13.5;

    public byte[] Generate(ProformaModel m)
    {
        using var book = new XLWorkbook();
        book.Properties.Title = m.Number is null ? $"Proforma invoice (draft) — {m.QuoteNumber}" : $"Proforma invoice {m.Number}";
        book.Properties.Author = "Jama Go Security Equipment";

        var ws = book.Worksheets.Add("Proforma Invoice");
        ws.ShowGridLines = false;
        ws.Style.Font.FontName = "Calibri";
        ws.Style.Font.FontSize = 10;
        ws.Style.Font.FontColor = Ink;
        ws.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        for (var c = 0; c < Columns; c++) ws.Column(c + 1).Width = Widths[c];

        var row = ComposeHeader(ws, m);
        row = ComposeBoxes(ws, m, row + 1);
        var headerRow = row + 1;
        row = ComposeItems(ws, m, headerRow);
        row = ComposeTotals(ws, m, row + 1);
        row = ComposeTermsAndPayment(ws, row + 2);
        ComposeCompany(ws, row + 2);

        var page = ws.PageSetup;
        page.PaperSize = XLPaperSize.A4Paper;
        page.PageOrientation = XLPageOrientation.Portrait;
        page.FitToPages(1, 0);
        page.Margins.Left = page.Margins.Right = 0.4;
        page.Margins.Top = page.Margins.Bottom = 0.5;
        page.CenterHorizontally = true;
        page.SetRowsToRepeatAtTop(headerRow, headerRow);
        page.Footer.Right.AddText("Page ");
        page.Footer.Right.AddText(XLHFPredefinedText.PageNumber);
        page.Footer.Right.AddText(" of ");
        page.Footer.Right.AddText(XLHFPredefinedText.NumberOfPages);

        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>The boxed title across the top (no logo in the workbook). Returns the last row used.</summary>
    private static int ComposeHeader(IXLWorksheet ws, ProformaModel m)
    {
        ws.Row(1).Height = 20;
        ws.Row(2).Height = 20;

        var title = ws.Range(1, 1, 2, Columns).Merge();
        title.Value = "PROFORMA INVOICE";
        title.Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(BrandStrong);
        title.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        title.Style.Border.SetOutsideBorder(XLBorderStyleValues.Medium).Border.SetOutsideBorderColor(BrandStrong);

        if (!m.Draft) return 2;
        var draft = ws.Range(3, 1, 3, Columns).Merge();
        draft.Value = "DRAFT — NOT APPROVED";
        draft.Style.Font.SetBold().Font.SetFontColor(DraftRed);
        draft.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        return 3;
    }

    /// <summary>The customer box (A:D) beside the references (labels E:F, values G:H). Returns the last row used.</summary>
    private static int ComposeBoxes(IXLWorksheet ws, ProformaModel m, int top)
    {
        var head = ws.Range(top, 1, top, 4).Merge();
        head.Value = "Customer Details:";
        head.Style.Font.SetBold().Fill.SetBackgroundColor(HeadBg);

        (string Label, string? Value)[] customer =
        [
            ("Name", m.ClientName), ("Project", m.ProjectName), ("Contact Number", m.ContactNumber), ("Location", m.Location),
        ];
        for (var i = 0; i < customer.Length; i++)
        {
            var cell = ws.Range(top + 1 + i, 1, top + 1 + i, 4).Merge().FirstCell();
            var text = cell.CreateRichText();
            text.AddText($"{customer[i].Label} : ").SetBold().SetFontColor(Muted);
            text.AddText(ProformaLook.Dash(customer[i].Value)).SetBold();
        }
        Box(ws.Range(top, 1, top + 4, 4));

        (string Label, string? Value)[] references =
        [
            ("Date#", ProformaLook.Date(m.Date)),
            ("PI No", m.Number ?? (m.Draft ? "DRAFT" : "Given on download")),
            ("Quote No#", m.QuoteNumber),
            ("Quote Date#", ProformaLook.Date(m.QuoteDate)),
            ("Ref#", m.Ref),
        ];
        for (var i = 0; i < references.Length; i++)
        {
            var r = top + i;
            var label = ws.Range(r, 5, r, 6).Merge();
            label.Value = references[i].Label;
            label.Style.Font.SetBold().Fill.SetBackgroundColor(HeadBg);
            var value = ws.Range(r, 7, r, Columns).Merge();
            value.Value = ProformaLook.Dash(references[i].Value);
            value.Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
        }
        Box(ws.Range(top, 5, top + 4, Columns));
        return top + 4;
    }

    /// <summary>The headings row and every section and line. Returns the last row used.</summary>
    private static int ComposeItems(IXLWorksheet ws, ProformaModel m, int headerRow)
    {
        string[] headings = ["SNO", "DESCRIPTION", "BRAND", "MODEL / PART NO.", "UNITS", "QTY", "UNIT PRICE", "TOTAL AMOUNT"];
        for (var c = 0; c < Columns; c++)
        {
            var cell = ws.Cell(headerRow, c + 1);
            cell.Value = headings[c];
            cell.Style.Font.SetBold().Font.SetFontSize(9).Fill.SetBackgroundColor(HeadBg)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetWrapText(true);
        }
        ws.Row(headerRow).Height = 18;

        var row = headerRow;
        foreach (var section in m.Sections)
        {
            if (!string.IsNullOrWhiteSpace(section.Title))
            {
                row++;
                var band = ws.Range(row, 1, row, Columns).Merge();
                band.Value = section.Title.Trim().ToUpperInvariant();
                band.Style.Font.SetBold().Font.SetFontColor(XLColor.Black).Fill.SetBackgroundColor(SectionBg)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }

            foreach (var line in section.Lines)
            {
                row++;
                ws.Cell(row, 1).Value = line.Number;
                ws.Cell(row, 2).Value = line.Description;
                ws.Cell(row, 3).Value = line.Brand ?? string.Empty;
                ws.Cell(row, 4).Value = line.Model ?? string.Empty;
                ws.Cell(row, 5).Value = ProformaText.Unit(line.Unit);
                ws.Cell(row, 6).Value = line.Quantity;
                ws.Cell(row, 7).Value = line.UnitPrice;
                ws.Cell(row, 8).Value = line.Total;

                ws.Range(row, 1, row, Columns).Style.Alignment.SetWrapText(true);
                foreach (var c in new[] { 1, 3, 4, 5, 6 })
                    ws.Cell(row, c).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                ws.Cell(row, 6).Style.NumberFormat.Format = "0.##";
                ws.Range(row, 7, row, 8).Style.NumberFormat.SetFormat(MoneyFormat)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);

                // Row heights are set, not left to the viewer: not every viewer re-measures wrapped text.
                var lines = new[] { Lines(line.Description, 46), Lines(line.Brand, 12), Lines(line.Model, 21) }.Max();
                ws.Row(row).Height = LineHeight * lines + 3;
            }
        }

        if (row == headerRow)
        {
            row++;
            var none = ws.Range(row, 1, row, Columns).Merge();
            none.Value = "This quotation has no items.";
            none.Style.Font.SetFontColor(Muted).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        }

        Grid(ws.Range(headerRow, 1, row, Columns));
        return row;
    }

    /// <summary>Notes and amount in words on the left, subtotal / discount / net total on the right.</summary>
    private static int ComposeTotals(IXLWorksheet ws, ProformaModel m, int top)
    {
        (string Label, decimal Value, bool Strong)[] totals =
        [
            ("Subtotal", m.Subtotal, false), ("Discount", m.Discount, false), ("Net Total (QAR)", m.NetTotal, true),
        ];
        for (var i = 0; i < totals.Length; i++)
        {
            var r = top + i;
            var label = ws.Range(r, 6, r, 7).Merge();
            label.Value = totals[i].Label;
            label.Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            var value = ws.Cell(r, Columns);
            value.Value = totals[i].Value;
            value.Style.Font.SetBold().NumberFormat.SetFormat(MoneyFormat).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            if (totals[i].Strong)
            {
                ws.Range(r, 6, r, Columns).Style.Fill.SetBackgroundColor(HeadBg).Font.SetFontSize(11);
                ws.Row(r).Height = 18;
            }
        }
        Grid(ws.Range(top, 6, top + 2, Columns));

        var po = ProformaLook.PoNote(m.PoNumber, m.PoDate);
        if (po is not null)
        {
            var note = ws.Range(top, 1, top, 4).Merge().FirstCell().CreateRichText();
            note.AddText("Notes: ").SetBold();
            note.AddText(po).SetBold();
        }

        var words = ws.Range(top + 1, 1, top + 2, 4).Merge();
        var rich = words.FirstCell().CreateRichText();
        rich.AddText("Amount In Words: ").SetBold();
        rich.AddText(MoneyWords.English(m.NetTotal)).SetItalic();
        words.Style.Alignment.SetWrapText(true).Alignment.SetVertical(XLAlignmentVerticalValues.Top);
        return top + 2;
    }

    /// <summary>Terms on the left (A:D), payment details on the right (E:H). Returns the last row used.</summary>
    private static int ComposeTermsAndPayment(IXLWorksheet ws, int top)
    {
        var termsHead = ws.Range(top, 1, top, 4).Merge();
        termsHead.Value = "Terms & Conditions:";
        termsHead.Style.Font.SetBold().Font.SetFontColor(BrandStrong).Font.SetFontSize(11);
        for (var i = 0; i < ProformaText.Terms.Count; i++)
        {
            var r = top + 1 + i;
            var term = ws.Range(r, 1, r, 4).Merge();
            term.Value = $"{i + 1}. {ProformaText.Terms[i]}";
            term.Style.Alignment.SetWrapText(true);
            ws.Row(r).Height = LineHeight * Lines(term.FirstCell().GetString(), 80) + 3;
        }

        var payHead = ws.Range(top, 5, top, Columns).Merge();
        payHead.Value = "Payment details as follows:";
        payHead.Style.Font.SetBold().Font.SetFontColor(BrandStrong).Font.SetFontSize(11);
        (string Label, string Value)[] payment =
        [
            ("Account Holder", CompanyPaymentDetails.AccountHolder),
            ("Account Number", CompanyPaymentDetails.AccountNumber),
            ("Bank", CompanyPaymentDetails.Bank),
            ("Branch", CompanyPaymentDetails.Branch),
            ("SWIFT Code", CompanyPaymentDetails.SwiftCode),
        ];
        for (var i = 0; i < payment.Length; i++)
        {
            var r = top + 1 + i;
            var label = ws.Range(r, 5, r, 6).Merge();
            label.Value = payment[i].Label;
            label.Style.Font.SetBold().Fill.SetBackgroundColor(HeadBg);
            var value = ws.Range(r, 7, r, Columns).Merge();
            value.Value = payment[i].Value;
            value.Style.Font.SetBold().Alignment.SetWrapText(true);
            var needed = LineHeight * Lines(payment[i].Value, 30) + 3;
            if (ws.Row(r).Height < needed) ws.Row(r).Height = needed;
        }
        Grid(ws.Range(top + 1, 5, top + payment.Length, Columns));
        return top + Math.Max(ProformaText.Terms.Count, payment.Length);
    }

    private static void ComposeCompany(IXLWorksheet ws, int top)
    {
        ws.Range(top, 1, top, Columns).Style.Border.SetTopBorder(XLBorderStyleValues.Medium).Border.SetTopBorderColor(Brand);
        (string Text, bool Strong)[] lines =
        [
            (ProformaText.CompanyName, true), (ProformaText.CompanyAddress, false), ($"Mobile {ProformaText.CompanyMobiles}", false),
        ];
        for (var i = 0; i < lines.Length; i++)
        {
            var cell = ws.Range(top + i, 1, top + i, Columns).Merge();
            cell.Value = lines[i].Text;
            cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            if (lines[i].Strong) cell.Style.Font.SetBold().Font.SetFontColor(BrandStrong).Font.SetFontSize(11);
            else cell.Style.Font.SetFontColor(Muted).Font.SetFontSize(9);
        }
    }

    private static void Box(IXLRange range) =>
        range.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorderColor(Border)
            .Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorderColor(Border);

    private static void Grid(IXLRange range) => Box(range);

    /// <summary>How many lines text takes in a column about <paramref name="perLine"/> characters wide.</summary>
    private static int Lines(string? text, int perLine) =>
        string.IsNullOrEmpty(text) ? 1 : Math.Max(1, (int)Math.Ceiling(text.Length / (double)perLine));
}
