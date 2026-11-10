using System.Globalization;
using System.Reflection;
using Jama.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Jama.Infrastructure.Documents;

/// <summary>
/// The bill a client receives when a Non-MOI maintenance job is closed: what was done, what each item cost, any
/// discount, and what is payable. Same house style as the contract — all running text black, one blue for the title,
/// table header, totals band and footer, even margins, the company letterhead and watermark.
/// </summary>
public sealed class NonMoiJobBillPdfGenerator : IJobBillPdfGenerator
{
    private static readonly Color Ink = Color.FromHex("#000000");
    private static readonly Color Blue = Color.FromHex("#1B4F9C");
    private static readonly Color Line = Color.FromHex("#C9D6EA");
    private static readonly Color Soft = Color.FromHex("#EAF0FA");
    private static readonly Color White = Color.FromHex("#FFFFFF");

    private const string CompanyPhone1 = "+974 4001 3599";
    private const string CompanyPhone2 = "+974 3064 4006";
    private const string CompanyEmail = "info@jamago.com";
    private const string CompanyAddress = "Showroom, Unit 41, Building 349, St 340, Zone 56, Al Ain Complex, Salwa Road";

    private static readonly byte[] LogoBytes = LoadEmbedded("Jama.Infrastructure.Documents.Assets.jamago-logo.png");
    private static readonly byte[] WatermarkBytes = LoadEmbedded("Jama.Infrastructure.Documents.Assets.jamago-quote-watermark.png");

    private static byte[] LoadEmbedded(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (stream is null) return [];
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

    public byte[] Generate(JobBillModel model)
    {
        DocumentFonts.EnsureRegistered();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(44);
                page.MarginTop(30);
                page.MarginBottom(24);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Ink).FontFamily(DocumentFonts.Arabic));

                page.Header().Element(c => ComposeHeader(c, model));
                page.Background().Element(c =>
                {
                    if (WatermarkBytes.Length > 0) c.AlignCenter().AlignMiddle().Width(300).Image(WatermarkBytes).FitWidth();
                });
                page.Content().PaddingTop(10).Column(content =>
                {
                    content.Item().Column(title =>
                    {
                        title.Item().AlignCenter().Text("فاتورة صيانة").FontFamily(DocumentFonts.Arabic).FontSize(24).ExtraBold().FontColor(Blue);
                        title.Item().AlignCenter().Text("Maintenance Bill").FontSize(15).Bold().FontColor(Blue);
                        title.Item().PaddingTop(6).AlignCenter().Width(150).Height(3).Background(Blue);
                    });

                    content.Item().PaddingTop(18).Element(c => ComposeCustomer(c, model));
                    content.Item().PaddingTop(16).Element(c => ComposeTable(c, model));
                    content.Item().PaddingTop(14).Element(c => ComposeTotals(c, model));
                    content.Item().PaddingTop(20).Element(ComposePayment);
                    content.Item().PaddingTop(22).AlignCenter().Text("Thank you for choosing Jama Go Security Equipment.")
                        .FontSize(10).Bold().FontColor(Blue);
                });
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, JobBillModel model)
    {
        container.Row(row =>
        {
            if (LogoBytes.Length > 0) row.ConstantItem(140).AlignMiddle().Image(LogoBytes).FitWidth();
            else row.ConstantItem(140).AlignMiddle().Text("JAMA GO").FontSize(12).Bold().FontColor(Blue);

            row.RelativeItem();

            row.ConstantItem(170).Border(1).BorderColor(Line).Padding(6).Column(col =>
            {
                col.Item().Text($"Job # : {model.JobNumber}").FontSize(8.5f).Bold();
                col.Item().Text($"Date : {model.Date:dd/MM/yyyy}").FontSize(8.5f);
                if (model.Revision > 1)
                    col.Item().Text($"Revision : {model.Revision}").FontSize(8.5f).Bold().FontColor(Blue);
            });
        });
    }

    private static void ComposeCustomer(IContainer container, JobBillModel model)
    {
        container.Background(Soft).Padding(10).Column(col =>
        {
            col.Item().Text("Billed to").FontSize(8.5f).Bold().FontColor(Blue);
            col.Item().PaddingTop(3).Text(model.CustomerName).FontSize(13).Bold();
            col.Item().PaddingTop(2).Text($"Phone: {model.CustomerNumber}");
            col.Item().Text($"Location: {model.Location}");
        });
    }

    private static void ComposeTable(IContainer container, JobBillModel model)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(28);
                columns.RelativeColumn(1);
                columns.ConstantColumn(36);
                columns.ConstantColumn(70);
                columns.ConstantColumn(80);
            });

            table.Header(header =>
            {
                Head(header.Cell(), "No.", false);
                Head(header.Cell(), "Description", false);
                Head(header.Cell(), "Qty", true);
                Head(header.Cell(), "Unit price", true);
                Head(header.Cell(), "Total (QAR)", true);
            });

            var number = 1;
            foreach (var line in model.Lines)
            {
                Body(table.Cell()).Text((number++).ToString(CultureInfo.InvariantCulture));
                Body(table.Cell()).Column(c =>
                {
                    c.Item().Text(line.Issue).Bold();
                    c.Item().PaddingTop(1).Text(line.Category).FontSize(8.5f);
                });
                Body(table.Cell()).AlignCenter().Text(line.Quantity.ToString(CultureInfo.InvariantCulture));
                Body(table.Cell()).AlignRight().Text(Money(line.UnitFee));
                Body(table.Cell()).AlignRight().Text(Money(line.LineTotal)).Bold();
            }
        });
    }

    private static void Head(IContainer cell, string text, bool right) =>
        cell.Background(Blue).PaddingVertical(7).PaddingHorizontal(6)
            .Element(c => (right ? c.AlignRight() : c.AlignLeft()).Text(text).FontSize(9).Bold().FontColor(White));

    private static IContainer Body(IContainer cell) =>
        cell.BorderBottom(0.75f).BorderColor(Line).PaddingVertical(7).PaddingHorizontal(6);

    private static void ComposeTotals(IContainer container, JobBillModel model)
    {
        container.AlignRight().Width(260).Column(col =>
        {
            Row(col, "Total", Money(model.Total));
            if (model.Discount > 0) Row(col, "Discount", "-" + Money(model.Discount));

            col.Item().PaddingTop(4).Background(Blue).PaddingVertical(9).PaddingHorizontal(10).Row(row =>
            {
                row.RelativeItem().Text("Amount payable (QAR)").FontSize(10).Bold().FontColor(White);
                row.AutoItem().Text(Money(model.AmountPayable)).FontSize(13).ExtraBold().FontColor(White);
            });
        });

        static void Row(ColumnDescriptor col, string label, string value) =>
            col.Item().BorderBottom(0.75f).BorderColor(Line).PaddingVertical(6).PaddingHorizontal(10).Row(row =>
            {
                row.RelativeItem().Text(label);
                row.AutoItem().Text(value).Bold();
            });
    }

    /// <summary>Where to pay. Same wording the email and the WhatsApp message carry.</summary>
    private static void ComposePayment(IContainer container)
    {
        container.Border(1.5f).BorderColor(Blue).Padding(12).Column(col =>
        {
            col.Item().Text("Payment details").FontSize(11).Bold().FontColor(Blue);
            col.Item().PaddingTop(5).Text(Jama.Application.Common.CompanyPaymentDetails.AccountName).FontSize(11).Bold();
            col.Item().PaddingTop(2).Text($"CR NO: {Jama.Application.Common.CompanyPaymentDetails.CrNumber}");
            col.Item().PaddingTop(2).Text(text =>
            {
                text.Span("IBAN: ");
                text.Span(Jama.Application.Common.CompanyPaymentDetails.Iban).Bold();
            });
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.PaddingTop(6).Background(Blue).PaddingVertical(9).PaddingHorizontal(16).Row(row =>
        {
            row.RelativeItem().Text($"{CompanyPhone1}   {CompanyPhone2}").FontSize(8).Bold().FontColor(White);
            row.RelativeItem().AlignCenter().Text(CompanyEmail).FontSize(8).Bold().FontColor(White);
            row.RelativeItem().AlignRight().Text(CompanyAddress).FontSize(7.5f).FontColor(White);
        });
    }
}
