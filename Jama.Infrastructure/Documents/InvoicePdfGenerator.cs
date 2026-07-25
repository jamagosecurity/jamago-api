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

                    column.Item().PaddingTop(4).Text($"Inspection details — Quarter {model.Quarter}")
                        .Bold().FontSize(13).FontColor(Colors.Grey.Darken3);

                    if (model.Cameras.Count > 0)
                    {
                        column.Item().PaddingTop(2).Text("Cameras").SemiBold().FontColor(Colors.Grey.Darken2);
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                            });

                            table.Header(header =>
                            {
                                foreach (var title in new[] { "Brand", "Model", "Qty", "Location", "Remarks" })
                                    header.Cell().Element(HeaderCell).Text(title);
                            });

                            foreach (var cam in model.Cameras)
                            {
                                table.Cell().Element(BodyCell).Text(cam.Brand);
                                table.Cell().Element(BodyCell).Text(cam.Model);
                                table.Cell().Element(BodyCell).Text(cam.Quantity.ToString());
                                table.Cell().Element(BodyCell).Text(cam.Location);
                                table.Cell().Element(BodyCell).Text(cam.Remarks);
                            }
                        });
                    }

                    foreach (var section in model.Sections)
                    {
                        column.Item().PaddingTop(6).Text(section.Title).SemiBold().FontColor(Colors.Grey.Darken2);
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(2);
                            });
                            foreach (var field in section.Fields)
                            {
                                table.Cell().Element(LabelCell).Text(field.Label);
                                table.Cell().Element(BodyCell).Text(field.Value);
                            }
                        });
                    }

                    if (model.Cameras.Count == 0 && model.Sections.Count == 0)
                        column.Item().PaddingTop(2).Text("No inspection details were recorded for this quarter.")
                            .Italic().FontColor(Colors.Grey.Darken1);

                    static IContainer HeaderCell(IContainer c) => c
                        .Background(Colors.Grey.Lighten3).Padding(6).DefaultTextStyle(x => x.SemiBold());
                    static IContainer BodyCell(IContainer c) => c
                        .BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6);
                    static IContainer LabelCell(IContainer c) => c
                        .BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6)
                        .DefaultTextStyle(x => x.FontColor(Colors.Grey.Darken1));
                });

                page.Footer().AlignCenter().Text(
                    "This document confirms completion of the quarterly inspection listed above. No pricing is reflected on this summary.")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });

        return document.GeneratePdf();
    }
}
