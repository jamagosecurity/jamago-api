using Jama.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Jama.Infrastructure.Documents;

public sealed class InvoicePdfGenerator : IInvoicePdfGenerator
{
    public byte[] Generate(InvoicePdfModel model)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Calibri));

                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(brand =>
                        {
                            brand.Item().Text("Jama Go Security").FontSize(20).Bold();
                            brand.Item().Text("Quarterly Inspection Invoice").FontSize(11).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(180).AlignRight().Column(meta =>
                        {
                            meta.Item().Text(model.InvoiceNumber).Bold().FontSize(13);
                            meta.Item().Text($"Generated: {model.GeneratedAt:dd MMM yyyy}").FontColor(Colors.Grey.Darken1);
                        });
                    });
                    column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(20).Column(column =>
                {
                    column.Spacing(16);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Client").Bold().FontColor(Colors.Grey.Darken2);
                            c.Item().Text(model.ClientName);
                            c.Item().Text(model.ClientLocation).FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"Client No: {model.ClientNumber}").FontColor(Colors.Grey.Darken1);
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("DIA Inspection").Bold().FontColor(Colors.Grey.Darken2);
                            c.Item().Text($"DIA No: {model.DiaNumber}");
                            c.Item().Text($"Quarter: Q{model.Quarter}");
                            c.Item().Text($"Technician: {model.TechnicianName}");
                        });
                    });

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Description");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Quarter");

                            static IContainer HeaderCell(IContainer c) => c
                                .Background(Colors.Grey.Lighten3)
                                .Padding(8)
                                .DefaultTextStyle(x => x.Bold());
                        });

                        table.Cell().Element(BodyCell).Text($"Quarterly security inspection — {model.DiaNumber}");
                        table.Cell().Element(BodyCell).AlignRight().Text($"Q{model.Quarter}");

                        static IContainer BodyCell(IContainer c) => c
                            .BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                            .Padding(8);
                    });
                });

                page.Footer().AlignCenter().Text(
                    "This document confirms completion of the quarterly inspection listed above. No pricing is reflected on this summary.")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });

        return document.GeneratePdf();
    }
}
