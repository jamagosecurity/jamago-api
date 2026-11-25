using System.Globalization;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Proformas;
using QuestPDF.Elements;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Jama.Infrastructure.Documents;

/// <summary>
/// Renders the proforma invoice of a quotation in the office's own layout: logo and boxed title, the customer box
/// beside the Date / PI No / Quote No / Quote Date / Ref box, then one table grouped by the quotation's sections with
/// a running item number, and the subtotal, discount, net total, P.O. note and amount in words at the foot.
/// No descriptions, pictures or terms — that is the quotation's job.
/// </summary>
public sealed class ProformaPdfGenerator : IProformaPdfGenerator
{
    private static readonly Color Brand = Color.FromHex("#2594D2");
    private static readonly Color BrandStrong = Color.FromHex("#1A7BB5");
    private static readonly Color Ink = Color.FromHex("#0F2846");
    private static readonly Color Muted = Color.FromHex("#5F7487");
    private static readonly Color Border = Color.FromHex("#9FB6CA");
    private static readonly Color HeadBg = Color.FromHex("#D9E9F6");
    private static readonly Color SectionBg = Color.FromHex("#EAF3FA");
    private static readonly Color DraftRed = Color.FromHex("#47D1273D");

    private static readonly byte[] LogoBytes = DocumentLook.Logo;

    private static string Money(decimal value) => DocumentLook.Money(value);
    private static string Date(DateOnly value) => DocumentLook.Date(value);
    private static string Qty(decimal value) => DocumentLook.Qty(value);
    private static string Unit(string uom) => ProformaText.Unit(uom);

    public byte[] Generate(ProformaModel model)
    {
        DocumentFonts.EnsureRegistered();

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(20);
            page.DefaultTextStyle(x => x.FontSize(8).FontColor(Ink).FontFamily(DocumentFonts.Body));
            if (model.Draft) page.Foreground().Element(ComposeDraftStamp);
            // Logo and title open the document: first page only.
            page.Header().ShowOnce().Element(c => ComposeHeader(c, model));
            page.Content().PaddingTop(6).Element(c => ComposeContent(c, model));
            // The company's name, address and mobiles close the document: drawn in the footer of the LAST page only.
            // Its space is kept on every page, so the block never has to start a page of its own.
            page.Footer().Column(footer =>
            {
                footer.Item().Dynamic(new LastPageCompany());
                footer.Item().Element(ComposePageNumber);
            });
        }))
        .WithMetadata(new DocumentMetadata
        {
            Title = model.Number is null ? $"Proforma invoice (draft) — {model.QuoteNumber}" : $"Proforma invoice {model.Number}",
            Author = "Jama Go Security Equipment",
        })
        .GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, ProformaModel model)
    {
        container.Row(row =>
        {
            if (LogoBytes.Length > 0)
                row.ConstantItem(150).AlignMiddle().Image(LogoBytes).FitWidth();
            else
                row.RelativeItem().Text("JAMA GO").FontSize(20).Bold().FontColor(Brand);

            row.RelativeItem().AlignRight().AlignMiddle()
                .Border(1.2f).BorderColor(BrandStrong).PaddingVertical(4).PaddingHorizontal(14)
                .Text("PROFORMA INVOICE").FontSize(14).Bold().FontColor(BrandStrong).LetterSpacing(0.04f);
        });
    }

    private static void ComposeContent(IContainer container, ProformaModel model)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Element(c => ComposeCustomer(c, model));
                row.ConstantItem(10);
                row.ConstantItem(190).Element(c => ComposeReferences(c, model));
            });

            column.Item().PaddingTop(6).Element(c => ComposeItems(c, model));
            column.Item().ShowEntire().Element(c => ComposeTotals(c, model));
            column.Item().ShowEntire().Element(ComposeTermsAndPayment);
        });
    }

    private static void ComposeCustomer(IContainer container, ProformaModel model)
    {
        container.Border(0.8f).BorderColor(Border).Column(column =>
        {
            column.Item().Background(HeadBg).PaddingVertical(1).PaddingHorizontal(6)
                .Text("Customer Details:").FontSize(8.5f).Bold();

            void Line(string label, string? value) =>
                column.Item().BorderTop(0.6f).BorderColor(Border).PaddingVertical(1).PaddingHorizontal(6).Text(text =>
                {
                    text.Span($"{label} : ").SemiBold().FontColor(Muted);
                    text.Span(string.IsNullOrWhiteSpace(value) ? "—" : value.Trim()).Bold();
                });

            Line("Name", model.ClientName);
            Line("Project", model.ProjectName);
            Line("Contact Number", model.ContactNumber);
            Line("Location", model.Location);
        });
    }

    private static void ComposeReferences(IContainer container, ProformaModel model)
    {
        container.Border(0.8f).BorderColor(Border).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(72);
                c.RelativeColumn();
            });

            void Pair(string label, string? value)
            {
                table.Cell().Border(0.6f).BorderColor(Border).Background(HeadBg).PaddingVertical(1).PaddingHorizontal(6)
                    .Text(label).SemiBold();
                table.Cell().Border(0.6f).BorderColor(Border).PaddingVertical(1).PaddingHorizontal(6).AlignRight()
                    .Text(string.IsNullOrWhiteSpace(value) ? "—" : value).Bold();
            }

            Pair("Date#", Date(model.Date));
            Pair("PI No", model.Number ?? (model.Draft ? "DRAFT" : "Given on download"));
            Pair("Quote No#", model.QuoteNumber);
            Pair("Quote Date#", Date(model.QuoteDate));
            Pair("Ref#", model.Ref);
        });
    }

    private static void ComposeItems(IContainer container, ProformaModel model)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(26);   // SNO
                c.RelativeColumn();     // description
                c.ConstantColumn(58);   // brand
                c.ConstantColumn(96);   // model / part no.
                c.ConstantColumn(34);   // units
                c.ConstantColumn(30);   // qty
                c.ConstantColumn(58);   // unit price
                c.ConstantColumn(66);   // total amount
            });

            table.Header(header =>
            {
                foreach (var title in new[] { "SNO", "DESCRIPTION", "BRAND", "MODEL / PART NO.", "UNITS", "QTY", "UNIT PRICE", "TOTAL AMOUNT" })
                    header.Cell().Border(0.6f).BorderColor(Border).Background(HeadBg).PaddingVertical(2).PaddingHorizontal(3)
                        .AlignCenter().AlignMiddle().Text(title).FontSize(7).Bold();
            });

            IContainer Cell(IContainer c) => c.Border(0.6f).BorderColor(Border).PaddingVertical(1).PaddingHorizontal(3).AlignMiddle()
                .DefaultTextStyle(x => x.FontSize(7.5f).LineHeight(1.1f));

            foreach (var section in model.Sections)
            {
                if (!string.IsNullOrWhiteSpace(section.Title))
                    table.Cell().ColumnSpan(8).Border(0.6f).BorderColor(Border).Background(SectionBg).PaddingVertical(1)
                        .AlignCenter().Text(section.Title.Trim().ToUpperInvariant()).FontSize(8.5f).Bold().FontColor(Colors.Black);

                foreach (var line in section.Lines)
                {
                    table.Cell().Element(Cell).AlignCenter().Text(line.Number.ToString(CultureInfo.InvariantCulture));
                    table.Cell().Element(Cell).Text(line.Description);
                    table.Cell().Element(Cell).AlignCenter().Text(line.Brand ?? string.Empty);
                    table.Cell().Element(Cell).AlignCenter().Text(line.Model ?? string.Empty);
                    table.Cell().Element(Cell).AlignCenter().Text(Unit(line.Unit));
                    table.Cell().Element(Cell).AlignCenter().Text(Qty(line.Quantity));
                    table.Cell().Element(Cell).AlignRight().Text(Money(line.UnitPrice));
                    table.Cell().Element(Cell).AlignRight().Text(Money(line.Total));
                }
            }
        });
    }

    private static void ComposeTotals(IContainer container, ProformaModel model)
    {
        container.PaddingTop(4).Row(row =>
        {
            row.RelativeItem().PaddingRight(12).Column(left =>
            {
                if (!string.IsNullOrWhiteSpace(model.PoNumber))
                {
                    left.Item().Text(text =>
                    {
                        text.Span("Notes: ").Bold();
                        text.Span($"P.O. No. {model.PoNumber}").Bold();
                        if (model.PoDate is { } poDate) text.Span($" dated {Date(poDate)}").Bold();
                    });
                }

                left.Item().PaddingTop(6).Text(text =>
                {
                    text.Span("Amount In Words: ").Bold();
                    text.Span(MoneyWords.English(model.NetTotal)).Italic();
                });
            });

            row.ConstantItem(220).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn();
                    c.ConstantColumn(96);
                });

                void Total(string label, string value, bool strong)
                {
                    var labelCell = table.Cell().Border(0.6f).BorderColor(Border).Background(strong ? HeadBg : Colors.White)
                        .PaddingVertical(1.5f).PaddingHorizontal(6).AlignRight();
                    var valueCell = table.Cell().Border(0.6f).BorderColor(Border).Background(strong ? HeadBg : Colors.White)
                        .PaddingVertical(1.5f).PaddingHorizontal(6).AlignRight();
                    if (strong)
                    {
                        labelCell.Text(label).FontSize(9).Bold();
                        valueCell.Text(value).FontSize(9).Bold();
                    }
                    else
                    {
                        labelCell.Text(label).SemiBold();
                        valueCell.Text(value).SemiBold();
                    }
                }

                Total("Subtotal", Money(model.Subtotal), false);
                Total("Discount", Money(model.Discount), false);
                Total("Net Total (QAR)", Money(model.NetTotal), true);
            });
        });
    }

    private static void ComposeDraftStamp(IContainer container)
    {
        container.AlignCenter().AlignMiddle().Rotate(-30).Text(text =>
        {
            text.AlignCenter();
            text.Line("DRAFT — NOT APPROVED").FontSize(40).Bold().FontColor(DraftRed);
            text.Line("مسودة — غير معتمدة").FontSize(22).Bold().FontColor(DraftRed);
        });
    }

    private static void ComposeTermsAndPayment(IContainer container)
    {
        container.PaddingTop(8).Row(row =>
        {
            row.RelativeItem().PaddingRight(14).Column(terms =>
            {
                terms.Item().Text("Terms & Conditions:").FontSize(9).Bold().FontColor(BrandStrong);
                for (var i = 0; i < ProformaText.Terms.Count; i++)
                {
                    var number = i + 1;
                    var line = ProformaText.Terms[i];
                    terms.Item().PaddingTop(2).Row(r =>
                    {
                        r.ConstantItem(14).Text($"{number}.").SemiBold();
                        r.RelativeItem().Text(line);
                    });
                }
            });

            row.ConstantItem(260).Column(pay =>
            {
                pay.Item().Text("Payment details as follows:").FontSize(9).Bold().FontColor(BrandStrong);
                pay.Item().PaddingTop(2).Border(0.8f).BorderColor(Border).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(86);
                        c.RelativeColumn();
                    });

                    void Pair(string label, string value)
                    {
                        table.Cell().Border(0.6f).BorderColor(Border).Background(HeadBg).PaddingVertical(1).PaddingHorizontal(6)
                            .Text(label).SemiBold();
                        table.Cell().Border(0.6f).BorderColor(Border).PaddingVertical(1).PaddingHorizontal(6)
                            .Text(value).Bold();
                    }

                    Pair("Account Holder", CompanyPaymentDetails.AccountHolder);
                    Pair("Account Number", CompanyPaymentDetails.AccountNumber);
                    Pair("Bank", CompanyPaymentDetails.Bank);
                    Pair("Branch", CompanyPaymentDetails.Branch);
                    Pair("SWIFT Code", CompanyPaymentDetails.SwiftCode);
                });
            });
        });
    }

    private static void ComposeCompany(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().PaddingBottom(3).Height(1.5f).Background(Brand);
            column.Item().AlignCenter().Text(ProformaText.CompanyName).FontSize(9).Bold().FontColor(BrandStrong).LetterSpacing(0.03f);
            column.Item().AlignCenter().Text(ProformaText.CompanyAddress).FontSize(7.5f).FontColor(Muted);
            column.Item().AlignCenter().Text($"Mobile {ProformaText.CompanyMobiles}").FontSize(7.5f).FontColor(Muted);
        });
    }

    /// <summary>The company block on the last page; the same height left empty on every other page.</summary>
    private sealed class LastPageCompany : IDynamicComponent
    {
        private const float Height = 46;

        public DynamicComponentComposeResult Compose(DynamicContext context)
        {
            var lastPage = context.PageNumber == context.TotalPages;
            var content = context.CreateElement(container =>
            {
                var box = container.Height(Height).AlignBottom();
                if (lastPage) box.Element(ComposeCompany);
            });
            return new DynamicComponentComposeResult { Content = content, HasMoreContent = false };
        }
    }

    private static void ComposePageNumber(IContainer container)
    {
        container.PaddingTop(4).AlignRight().Text(text =>
        {
            text.DefaultTextStyle(x => x.FontSize(7).FontColor(Muted));
            text.Span("Page ");
            text.CurrentPageNumber();
            text.Span(" of ");
            text.TotalPages();
        });
    }
}
