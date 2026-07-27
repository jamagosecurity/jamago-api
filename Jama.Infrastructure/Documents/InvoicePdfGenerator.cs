using System.Reflection;
using Jama.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Jama.Infrastructure.Documents;

public sealed class InvoicePdfGenerator : IInvoicePdfGenerator
{
    // Jama Go brand palette — kept in sync with the web app's CSS custom properties
    // (--brand / --brand-strong / --brand-2 / --brand-2-strong in styles.css).
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
    private static readonly Color SealBorder = Color.FromHex("#C8DAE8");

    // Each inspection discipline gets its own accent so sections stay
    // distinguishable at a glance when an invoice runs over several pages.
    private static readonly Color[] SectionAccents =
    [
        Color.FromHex("#1A7BB5"), // blue    — Network
        Color.FromHex("#7C5CD6"), // violet  — VMS
        Color.FromHex("#0E9F8C"), // teal    — UPS / General
        Color.FromHex("#D9822B"), // amber   — ANPR
        Color.FromHex("#C2456B"), // rose    — K'Poi
    ];

    private static Color AccentFor(int index) => SectionAccents[index % SectionAccents.Length];

    private static readonly byte[] LogoBytes =
        LoadEmbedded("Jama.Infrastructure.Documents.Assets.jamago-logo.png");

    // Pre-faded copy of the logo — QuestPDF has no opacity filter for raster
    // images, so the transparency is baked into the asset itself.
    private static readonly byte[] WatermarkBytes =
        LoadEmbedded("Jama.Infrastructure.Documents.Assets.jamago-logo-watermark.png");

    private static byte[] LoadEmbedded(string resourceName)
    {
        var assembly = typeof(InvoicePdfGenerator).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return [];

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    public byte[] Generate(InvoicePdfModel model) => BuildDocument(model).GeneratePdf();

    private static QuestPDF.Infrastructure.IDocument BuildDocument(InvoicePdfModel model)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.PageColor(White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Calibri).FontColor(Ink));

                page.Foreground().Element(ComposeWatermark);
                page.Header().Element(c => ComposeHeader(c, model));
                page.Content().Element(c => ComposeContent(c, model));
                page.Footer().Element(ComposeFooter);
            });
        });
    }

    private static void ComposeWatermark(IContainer container)
    {
        if (WatermarkBytes.Length == 0)
            return;

        container.AlignCenter().AlignMiddle().Width(470).Image(WatermarkBytes).FitWidth();
    }

    private static void ComposeHeader(IContainer container, InvoicePdfModel model)
    {
        container.Column(column =>
        {
            column.Item().PaddingHorizontal(40).PaddingTop(32).PaddingBottom(20).Row(row =>
            {
                if (LogoBytes.Length > 0)
                {
                    row.ConstantItem(108).Height(56).Image(LogoBytes).FitArea();
                    row.ConstantItem(18);
                }

                row.RelativeItem().Column(brand =>
                {
                    brand.Item().Text("JAMA GO SECURITY EQUIPMENT").FontSize(15).Bold().FontColor(Ink)
                        .LetterSpacing(0.01f);
                    brand.Item().PaddingTop(3).Text("Quarterly Inspection Invoice").FontSize(10)
                        .FontColor(Muted);
                    brand.Item().PaddingTop(8).Text("Unit 41, Zone 56, Street 340, Al Ain Complex, Salwa Road, Doha, Qatar")
                        .FontSize(8).FontColor(Muted);
                });

                row.ConstantItem(180).Column(meta =>
                {
                    meta.Item().AlignRight().Text("INVOICE NO.").FontSize(8).Bold().FontColor(Muted)
                        .LetterSpacing(0.1f);
                    meta.Item().AlignRight().PaddingTop(2).Text(model.InvoiceNumber).FontSize(15).Bold()
                        .FontColor(BrandStrong);
                    meta.Item().AlignRight().PaddingTop(8).Text($"Generated {model.GeneratedAt:dd MMM yyyy}")
                        .FontSize(8.5f).FontColor(Muted);
                    meta.Item().AlignRight().PaddingTop(6).Element(c => c
                        .Background(Accent)
                        .CornerRadius(4)
                        .PaddingVertical(4)
                        .PaddingHorizontal(10)
                        .Text($"QUARTER {model.Quarter}")
                        .FontSize(8).Bold().FontColor(White).LetterSpacing(0.04f));
                });
            });

            // Full-bleed two-tone brand rule.
            column.Item().Height(4).Row(row =>
            {
                row.RelativeItem(3).Background(Brand);
                row.RelativeItem(1).Background(Accent);
            });
        });
    }

    private static void ComposeContent(IContainer container, InvoicePdfModel model)
    {
        container.PaddingHorizontal(40).PaddingTop(24).PaddingBottom(12).Column(column =>
        {
            column.Spacing(18);

            column.Item().Row(row =>
            {
                row.RelativeItem().Element(c => InfoPanel(c, Brand, "CLIENT", panel =>
                {
                    panel.Item().Text(model.ClientName).FontSize(13).Bold().FontColor(Ink);
                    panel.Item().PaddingTop(4).Text(model.ClientLocation).FontSize(9.5f).FontColor(InkSoft);
                    panel.Item().PaddingTop(6).Text($"Client No: {model.ClientNumber}").FontSize(9)
                        .FontColor(Muted);
                }));

                row.ConstantItem(16);

                row.RelativeItem().Element(c => InfoPanel(c, Accent, "DIA INSPECTION", panel =>
                {
                    panel.Item().Text($"DIA No: {model.DiaNumber}").FontSize(13).Bold().FontColor(Ink);
                    panel.Item().PaddingTop(4).Text($"Quarter {model.Quarter} inspection cycle").FontSize(9.5f)
                        .FontColor(InkSoft);
                    panel.Item().PaddingTop(6).Text($"Technician: {model.TechnicianName}").FontSize(9)
                        .FontColor(Muted);
                }));
            });

            column.Item().Element(c => SectionTitle(c, "Inspection Details", $"Quarter {model.Quarter}"));

            if (model.Cameras.Count > 0)
            {
                column.Item().Element(c => SectionPanel(
                    c, Brand, "Cameras", $"{model.Cameras.Count} recorded",
                    inner => CamerasTable(inner, model)));
            }

            for (var i = 0; i < model.Sections.Count; i++)
            {
                var section = model.Sections[i];
                var accent = AccentFor(i);
                column.Item().Element(c => DetailSection(c, section, accent));
            }

            if (model.Cameras.Count == 0 && model.Sections.Count == 0)
                column.Item().Background(RowAlt).CornerRadius(8).Padding(16).AlignCenter()
                    .Text("No inspection details were recorded for this quarter.")
                    .Italic().FontSize(9.5f).FontColor(Muted);

            column.Item().PaddingTop(4).Background(PanelBg).BorderLeft(3).BorderColor(Brand)
                .Padding(12).Text(
                    "This document confirms completion of the quarterly inspection listed above. No pricing is reflected on this summary.")
                .FontSize(8.5f).FontColor(InkSoft);

            column.Item().PaddingTop(6).Element(c => ComposeSignatures(c, model));
        });
    }

    private static void InfoPanel(IContainer container, Color accent, string label, Action<ColumnDescriptor> body)
    {
        container.Border(1).BorderColor(accent.WithAlpha((byte)90)).CornerRadius(8).Column(panel =>
        {
            // Solid coloured caption band so the two panels read as distinct blocks.
            panel.Item().Background(accent)
                .CornerRadiusTopLeft(7).CornerRadiusTopRight(7)
                .PaddingVertical(6).PaddingHorizontal(13)
                .Text(label).FontSize(8.5f).Bold().FontColor(White).LetterSpacing(0.1f);

            panel.Item().Background(PanelBg)
                .CornerRadiusBottomLeft(7).CornerRadiusBottomRight(7)
                .PaddingVertical(12).PaddingHorizontal(13)
                .Column(body);
        });
    }

    /// <summary>
    /// Draws a titled, bordered panel in the section's accent colour. <paramref name="body"/>
    /// renders inside the panel so each discipline reads as one self-contained block.
    /// </summary>
    private static void SectionPanel(
        IContainer container,
        Color accent,
        string title,
        string? tag,
        Action<IContainer> body)
    {
        container.Border(1).BorderColor(accent.WithAlpha((byte)80)).CornerRadius(8).Column(panel =>
        {
            panel.Item()
                .Background(accent.WithAlpha((byte)30))
                .BorderBottom(1).BorderColor(accent.WithAlpha((byte)80))
                .CornerRadiusTopLeft(7).CornerRadiusTopRight(7)
                .PaddingVertical(7).PaddingHorizontal(12)
                .Row(row =>
                {
                    row.ConstantItem(4).Height(13).AlignMiddle().Background(accent).CornerRadius(2);
                    row.ConstantItem(8);
                    row.AutoItem().AlignMiddle().Text(title).FontSize(11).Bold().FontColor(accent);

                    row.RelativeItem();

                    if (!string.IsNullOrEmpty(tag))
                    {
                        row.AutoItem().AlignMiddle().Element(c => c
                            .Background(accent)
                            .CornerRadius(3)
                            .PaddingVertical(2)
                            .PaddingHorizontal(7)
                            .Text(tag.ToUpperInvariant())
                            .FontSize(7).Bold().FontColor(White).LetterSpacing(0.06f));
                    }
                });

            panel.Item().Padding(2).Element(body);
        });
    }

    private static void SectionTitle(IContainer container, string title, string? tag = null)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.AutoItem().Text(title).FontSize(12.5f).Bold().FontColor(Ink);
                if (!string.IsNullOrEmpty(tag))
                {
                    row.ConstantItem(10);
                    row.AutoItem().AlignMiddle().Text(tag.ToUpperInvariant()).FontSize(8).Bold()
                        .FontColor(Muted).LetterSpacing(0.08f);
                }
            });
            column.Item().PaddingTop(6).Height(2).Width(46).Background(Accent);
        });
    }

    private static void CamerasTable(IContainer container, InvoicePdfModel model)
    {
        container.Table(table =>
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
                    header.Cell().Element(HeaderCell).Text(title).FontSize(8.5f).Bold().FontColor(White)
                        .LetterSpacing(0.03f);
            });

            for (var i = 0; i < model.Cameras.Count; i++)
            {
                var cam = model.Cameras[i];
                var cell = i % 2 == 0 ? (Func<IContainer, IContainer>)BodyCell : BodyCellAlt;

                table.Cell().Element(cell).Text(cam.Brand).FontSize(9.5f);
                table.Cell().Element(cell).Text(cam.Model).FontSize(9.5f);
                table.Cell().Element(cell).Text(cam.Quantity.ToString()).FontSize(9.5f);
                table.Cell().Element(cell).Text(cam.Location).FontSize(9.5f);
                table.Cell().Element(cell).Text(cam.Remarks).FontSize(9.5f);
            }
        });
    }

    private static void DetailSection(IContainer container, InvoicePdfSection section, Color accent)
    {
        SectionPanel(container, accent, section.Title, $"{section.Fields.Count} fields", inner =>
            inner.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(2);
                });

                for (var i = 0; i < section.Fields.Count; i++)
                {
                    var field = section.Fields[i];
                    var (labelCell, valueCell) = i % 2 == 0
                        ? ((Func<IContainer, IContainer>)LabelCell, (Func<IContainer, IContainer>)BodyCell)
                        : (LabelCellAlt, BodyCellAlt);

                    table.Cell().Element(labelCell).Text(field.Label).FontSize(9);
                    table.Cell().Element(valueCell).Text(field.Value).FontSize(9.5f);
                }
            }));
    }

    private static void ComposeSignatures(IContainer container, InvoicePdfModel model)
    {
        // Keep the whole sign-off together — a signature panel split across a
        // page break is not valid for sign-off.
        container.PreventPageBreak().Element(c => SectionPanel(
            c, BrandStrong, "Verification & Sign-off", "authorised", inner =>
                inner.Padding(12).Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        // Signatures on the left, stamp area reserved on the right.
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Element(c => SignatureBox(
                                c, Brand, "INSPECTED BY", model.TechnicianName, "Jama Go Technician"));

                            left.Item().PaddingTop(10).Element(c => SignatureBox(
                                c, Accent, "RECEIVED / ACCEPTED BY", model.ClientName,
                                "Client Representative"));
                        });

                        row.ConstantItem(18);

                        row.ConstantItem(190).Column(right =>
                        {
                            right.Item().Text("COMPANY STAMP & OFFICIAL SEAL")
                                .FontSize(7.5f).Bold().FontColor(BrandStrong).LetterSpacing(0.08f);

                            right.Item().PaddingTop(6).Height(150)
                                .Border(1).BorderColor(SealBorder).CornerRadius(8)
                                .Background(PanelBg)
                                .AlignCenter().AlignMiddle()
                                .Text("affix stamp here")
                                .FontSize(7.5f).Italic().FontColor(Muted);
                        });
                    });

                    column.Item().PaddingTop(10)
                        .Text("Authorised on behalf of Jama Go Security Equipment, Doha, Qatar.")
                        .FontSize(8).FontColor(Muted);
                })));
    }

    private static void SignatureBox(IContainer container, Color accent, string label, string name, string role)
    {
        container.Border(1).BorderColor(PanelBorder).CornerRadius(8).Padding(12).Column(box =>
        {
            box.Item().Text(label).FontSize(7.5f).Bold().FontColor(accent).LetterSpacing(0.1f);

            // Blank ruled area for the wet signature.
            box.Item().PaddingTop(26).BorderBottom(1).BorderColor(Muted.WithAlpha((byte)110));

            box.Item().PaddingTop(6).Text(string.IsNullOrWhiteSpace(name) || name == "-" ? " " : name)
                .FontSize(9.5f).SemiBold().FontColor(Ink);
            box.Item().Text(role).FontSize(8).FontColor(Muted);

            box.Item().PaddingTop(8).Row(r =>
            {
                r.AutoItem().AlignMiddle().Text("Date:").FontSize(8).FontColor(Muted);
                r.ConstantItem(6);
                r.RelativeItem().PaddingTop(9).BorderBottom(1).BorderColor(Muted.WithAlpha((byte)90));
            });
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.BorderTop(1).BorderColor(Line).PaddingHorizontal(40).PaddingTop(10).PaddingBottom(22)
            .Column(column =>
            {
                column.Spacing(3);
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text(text =>
                    {
                        text.DefaultTextStyle(x => x.FontSize(8).FontColor(Muted));
                        text.Span("Jama Go Security Equipment").FontColor(InkSoft).SemiBold();
                        text.Span("  ·  info@jamago.qa  ·  +974 3064 4006  ·  jamago.qa");
                    });

                    row.ConstantItem(110).AlignRight().Text(text =>
                    {
                        text.DefaultTextStyle(x => x.FontSize(8).FontColor(Muted));
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });
            });
    }

    private static IContainer HeaderCell(IContainer c) => c
        .Background(BrandStrong).PaddingVertical(8).PaddingHorizontal(6);

    private static IContainer BodyCell(IContainer c) => c
        .Background(White).BorderBottom(1).BorderColor(Line).PaddingVertical(7).PaddingHorizontal(6);

    private static IContainer BodyCellAlt(IContainer c) => c
        .Background(RowAlt).BorderBottom(1).BorderColor(Line).PaddingVertical(7).PaddingHorizontal(6);

    private static IContainer LabelCell(IContainer c) => c
        .Background(White).BorderBottom(1).BorderColor(Line).PaddingVertical(7).PaddingHorizontal(6)
        .DefaultTextStyle(x => x.FontColor(Muted));

    private static IContainer LabelCellAlt(IContainer c) => c
        .Background(RowAlt).BorderBottom(1).BorderColor(Line).PaddingVertical(7).PaddingHorizontal(6)
        .DefaultTextStyle(x => x.FontColor(Muted));
}
