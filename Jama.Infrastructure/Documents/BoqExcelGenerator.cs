using ClosedXML.Excel;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Q = Jama.Infrastructure.Documents.BoqPdfGenerator;

namespace Jama.Infrastructure.Documents;

/// <summary>
/// The quotation's priced items as an Excel workbook: who it is for, then every section with its items (brand, type,
/// model, item, description, unit, quantity, unit price, total) and section total, then the total, discount and final
/// amount with the amount in words. No cover, letter, logo or pictures — Excel is for the figures; Word carries the
/// whole document. Values, not formulas, so every viewer shows the PDF's figures. A4 landscape, one page wide, the
/// table headings repeated on every printed page.
/// </summary>
public sealed class BoqExcelGenerator : IBoqExcelGenerator
{
    private static readonly XLColor BrandStrong = XLColor.FromHtml("#" + DocumentLook.BrandStrong);
    private static readonly XLColor Brand = XLColor.FromHtml("#" + DocumentLook.Brand);
    private static readonly XLColor Ink = XLColor.FromHtml("#" + DocumentLook.Ink);
    private static readonly XLColor Muted = XLColor.FromHtml("#" + DocumentLook.Muted);
    private static readonly XLColor Accent = XLColor.FromHtml("#" + DocumentLook.Accent);
    private static readonly XLColor Border = XLColor.FromHtml("#" + DocumentLook.Border);
    private static readonly XLColor HeadBg = XLColor.FromHtml("#" + DocumentLook.HeadBg);
    private static readonly XLColor SectionBg = XLColor.FromHtml("#" + DocumentLook.SectionBg);
    private static readonly XLColor PanelBg = XLColor.FromHtml("#" + DocumentLook.PanelBg);
    private static readonly XLColor DraftRed = XLColor.FromHtml("#" + DocumentLook.DraftRed);

    private const int Columns = 10;
    //                                  S.No Brand Type Model Item Desc Unit Qty Price Total
    private static readonly double[] Widths = [6, 13, 11, 18, 30, 50, 7, 7, 13, 15];
    private const string MoneyFormat = "#,##0.00";
    private const double LineHeight = 13.5;

    public byte[] Generate(BoqPdfModel m)
    {
        using var book = new XLWorkbook();
        book.Properties.Title = $"Quotation {Q.NumberWithRevision(m)}";
        book.Properties.Author = Q.CompanyName;

        var ws = book.Worksheets.Add("Quotation");
        ws.ShowGridLines = false;
        ws.Style.Font.FontName = "Calibri";
        ws.Style.Font.FontSize = 10;
        ws.Style.Font.FontColor = Ink;
        ws.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        for (var c = 0; c < Columns; c++) ws.Column(c + 1).Width = Widths[c];

        var row = ComposeTitle(ws, m);
        row = ComposeDetails(ws, m, row + 2);
        var headerRow = row + 2;
        row = ComposeItems(ws, m, headerRow);
        row = ComposeTotals(ws, m, row + 2);
        if (!string.IsNullOrWhiteSpace(m.Notes)) row = ComposeNotes(ws, m.Notes!, row + 2);
        ComposeCompany(ws, row + 2);

        var page = ws.PageSetup;
        page.PaperSize = XLPaperSize.A4Paper;
        page.PageOrientation = XLPageOrientation.Landscape;
        page.FitToPages(1, 0);
        page.Margins.Left = page.Margins.Right = 0.4;
        page.Margins.Top = page.Margins.Bottom = 0.5;
        page.CenterHorizontally = true;
        page.SetRowsToRepeatAtTop(headerRow, headerRow);
        page.Footer.Left.AddText($"{Q.CompanyName} · {Q.NumberWithRevision(m)}");
        page.Footer.Right.AddText("Page ");
        page.Footer.Right.AddText(XLHFPredefinedText.PageNumber);
        page.Footer.Right.AddText(" of ");
        page.Footer.Right.AddText(XLHFPredefinedText.NumberOfPages);

        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    private static int ComposeTitle(IXLWorksheet ws, BoqPdfModel m)
    {
        var title = ws.Range(1, 1, 1, Columns).Merge();
        title.Value = "QUOTATION";
        title.Style.Font.SetBold().Font.SetFontSize(18).Font.SetFontColor(BrandStrong)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Row(1).Height = 26;

        var number = ws.Range(2, 1, 2, Columns).Merge().FirstCell().CreateRichText();
        number.AddText("Quotation no  ").SetFontColor(Muted);
        number.AddText(Q.NumberWithRevision(m)).SetBold().SetFontSize(12);
        ws.Range(2, 1, 2, Columns).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Border.SetBottomBorder(XLBorderStyleValues.Medium).Border.SetBottomBorderColor(Brand);

        if (!m.Watermark) return 2;
        var draft = ws.Range(3, 1, 3, Columns).Merge();
        draft.Value = "DRAFT — NOT APPROVED";
        draft.Style.Font.SetBold().Font.SetFontColor(DraftRed).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        return 3;
    }

    /// <summary>To / Date, Project / Location, Contact number / Prepared by, Valid until. Labels in A:B and F, values
    /// in C:E and G:J.</summary>
    private static int ComposeDetails(IXLWorksheet ws, BoqPdfModel m, int top)
    {
        (string, string?, string, string?)[] pairs =
        [
            ("TO", m.ClientName, "DATE", Q.Date(m.IssueDate)),
            ("PROJECT", m.ProjectName, "LOCATION", m.SiteLocation),
            ("CONTACT NUMBER", m.ContactNumber, "PREPARED BY", Q.PreparedBy(m.PreparedByName)),
        ];
        for (var i = 0; i < pairs.Length; i++)
        {
            var r = top + i;
            Label(ws.Range(r, 1, r, 2), pairs[i].Item1);
            Value(ws.Range(r, 3, r, 5), pairs[i].Item2);
            Label(ws.Range(r, 6, r, 6), pairs[i].Item3);
            Value(ws.Range(r, 7, r, Columns), pairs[i].Item4);
        }

        var valid = top + pairs.Length;
        Label(ws.Range(valid, 1, valid, 2), "VALID UNTIL");
        var until = ws.Range(valid, 3, valid, Columns).Merge().FirstCell().CreateRichText();
        until.AddText(Q.Date(m.IssueDate.AddDays(Q.ValidityDays))).SetBold().SetFontColor(Accent);
        until.AddText($"     Valid for {Q.ValidityDays} days (2 weeks) from the date above.").SetFontColor(Muted).SetFontSize(9);

        Grid(ws.Range(top, 1, valid, Columns));
        return valid;
    }

    private static void Label(IXLRange range, string text)
    {
        range.Merge().Value = text;
        range.Style.Font.SetBold().Font.SetFontSize(8).Font.SetFontColor(Muted).Fill.SetBackgroundColor(PanelBg);
    }

    private static void Value(IXLRange range, string? text)
    {
        range.Merge().Value = string.IsNullOrWhiteSpace(text) ? "—" : text.Trim();
        range.Style.Font.SetBold();
    }

    private static int ComposeItems(IXLWorksheet ws, BoqPdfModel m, int headerRow)
    {
        string[] headings = ["S.No", "Brand", "Type", "Model", "Item", "Description", "Unit", "Qty", "Unit Price", "Total Amount"];
        for (var c = 0; c < Columns; c++)
        {
            var cell = ws.Cell(headerRow, c + 1);
            cell.Value = headings[c];
            cell.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Font.SetFontSize(9).Fill.SetBackgroundColor(BrandStrong)
                .Alignment.SetHorizontal(c >= 6 ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left);
        }
        ws.Row(headerRow).Height = 18;

        var row = headerRow;
        foreach (var section in m.Sections)
        {
            row++;
            var band = ws.Range(row, 1, row, Columns).Merge();
            band.Value = $"{section.Number}.  {section.Title}";
            band.Style.Font.SetBold().Font.SetFontSize(11).Font.SetFontColor(BrandStrong).Fill.SetBackgroundColor(SectionBg)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Row(row).Height = 18;

            foreach (var line in section.Lines)
            {
                row++;
                var description = string.Join("\n", new[] { line.Description, line.DescriptionAr }
                    .Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => d!.Trim()));

                ws.Cell(row, 1).Value = line.Number;
                ws.Cell(row, 2).Value = line.Brand ?? string.Empty;
                ws.Cell(row, 3).Value = line.Type ?? string.Empty;
                ws.Cell(row, 4).Value = line.ModelNo ?? string.Empty;
                ws.Cell(row, 5).Value = line.ItemName;
                ws.Cell(row, 6).Value = description;
                ws.Cell(row, 7).Value = Q.Unit(line.Uom);
                ws.Cell(row, 8).Value = line.Quantity;
                ws.Cell(row, 9).Value = line.UnitRate;
                ws.Cell(row, 10).Value = line.LineTotal;

                var cells = ws.Range(row, 1, row, Columns);
                cells.Style.Alignment.SetWrapText(true).Alignment.SetVertical(XLAlignmentVerticalValues.Top);
                ws.Cell(row, 1).Style.Font.SetFontColor(Muted);
                ws.Cell(row, 5).Style.Font.SetBold();
                ws.Cell(row, 6).Style.Font.SetFontSize(9).Font.SetFontColor(Muted);
                ws.Range(row, 7, row, Columns).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                ws.Cell(row, 8).Style.NumberFormat.Format = "0.##";
                ws.Range(row, 9, row, Columns).Style.NumberFormat.SetFormat(MoneyFormat);
                ws.Cell(row, 10).Style.Font.SetBold();

                // Row heights are set, not left to the viewer: not every viewer re-measures wrapped text.
                var lines = new[]
                {
                    Lines(line.ItemName, 29), Lines(line.Description, 55) + Lines(line.DescriptionAr, 55) - (string.IsNullOrWhiteSpace(line.DescriptionAr) ? 1 : 0),
                    Lines(line.ModelNo, 17), Lines(line.Brand, 12), Lines(line.Type, 10),
                }.Max();
                ws.Row(row).Height = LineHeight * lines + 4;
            }

            row++;
            var label = ws.Range(row, 1, row, Columns - 1).Merge();
            label.Value = "Section total";
            label.Style.Font.SetBold().Font.SetFontSize(9).Font.SetFontColor(Muted).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            var subtotal = ws.Cell(row, Columns);
            subtotal.Value = section.Subtotal;
            subtotal.Style.Font.SetBold().Font.SetFontColor(BrandStrong).NumberFormat.SetFormat(MoneyFormat)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
        }

        Grid(ws.Range(headerRow, 1, row, Columns));
        return row;
    }

    private static int ComposeTotals(IXLWorksheet ws, BoqPdfModel m, int top)
    {
        var discounted = m.SpecialDiscount > 0;
        var rows = new List<(string Label, string Arabic, decimal Value, bool Strong, bool Minus)>
        {
            ("Total", "الإجمالي", m.Total, false, false),
        };
        if (discounted) rows.Add(("Discount", "الخصم", m.SpecialDiscount, false, true));
        rows.Add((discounted ? "FINAL AMOUNT (QAR)" : "TOTAL (QAR)", discounted ? "المبلغ النهائي (ر.ق)" : "الإجمالي (ر.ق)", m.GrandTotal, true, false));

        for (var i = 0; i < rows.Count; i++)
        {
            var r = top + i;
            // Labels across Description to Unit Price (F:I): "FINAL AMOUNT (QAR)" and its Arabic need the room.
            var label = ws.Range(r, 6, r, 9).Merge();
            label.Value = $"{rows[i].Label}   {rows[i].Arabic}";
            label.Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            var value = ws.Cell(r, Columns);
            value.Value = rows[i].Minus ? -rows[i].Value : rows[i].Value;
            value.Style.Font.SetBold().NumberFormat.SetFormat(MoneyFormat).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            if (rows[i].Minus) value.Style.Font.SetFontColor(Accent);
            if (rows[i].Strong)
            {
                ws.Range(r, 6, r, Columns).Style.Fill.SetBackgroundColor(BrandStrong).Font.SetFontColor(XLColor.White).Font.SetFontSize(11);
                ws.Row(r).Height = 20;
            }
        }
        Grid(ws.Range(top, 6, top + rows.Count - 1, Columns));

        var english = ws.Range(top, 1, top, 5).Merge().FirstCell().CreateRichText();
        english.AddText("AMOUNT IN WORDS  ").SetBold().SetFontSize(8).SetFontColor(Muted);
        english.AddText(MoneyWords.English(m.GrandTotal)).SetBold();
        var arabicRange = ws.Range(top + 1, 1, top + 1, 5).Merge();
        var arabic = arabicRange.FirstCell().CreateRichText();
        arabic.AddText("المبلغ كتابةً  ").SetBold().SetFontSize(8).SetFontColor(Muted);
        arabic.AddText(MoneyWords.Arabic(m.GrandTotal)).SetBold();
        arabicRange.Style.Alignment.SetReadingOrder(XLAlignmentReadingOrderValues.RightToLeft)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
        return top + rows.Count - 1;
    }

    private static int ComposeNotes(IXLWorksheet ws, string notes, int top)
    {
        var head = ws.Range(top, 1, top, Columns).Merge();
        head.Value = "NOTES";
        head.Style.Font.SetBold().Font.SetFontSize(8).Font.SetFontColor(Muted);
        var body = ws.Range(top + 1, 1, top + 1, Columns).Merge();
        body.Value = notes.Trim();
        body.Style.Alignment.SetWrapText(true).Alignment.SetVertical(XLAlignmentVerticalValues.Top);
        ws.Row(top + 1).Height = LineHeight * notes.Split('\n').Sum(l => Lines(l, 180)) + 4;
        return top + 1;
    }

    private static void ComposeCompany(IXLWorksheet ws, int top)
    {
        ws.Range(top, 1, top, Columns).Style.Border.SetTopBorder(XLBorderStyleValues.Medium).Border.SetTopBorderColor(Brand);
        (string Text, bool Strong)[] lines =
        [
            (Q.CompanyName, true),
            ($"{Q.CompanyAddress}, {Q.CompanyCity}", false),
            ($"{Q.CompanyPhone1} | {Q.CompanyPhone2} | {Q.CompanyPhone3} · {Q.CompanyEmail} · {Q.CompanyWebsite}", false),
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

    private static void Grid(IXLRange range) =>
        range.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorderColor(Border)
            .Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorderColor(Border);

    private static int Lines(string? text, int perLine) =>
        string.IsNullOrEmpty(text) ? 1 : text.Split('\n').Sum(part => Math.Max(1, (int)Math.Ceiling(part.Length / (double)perLine)));
}
