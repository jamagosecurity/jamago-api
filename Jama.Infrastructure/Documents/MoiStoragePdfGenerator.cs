using System.Globalization;
using System.Reflection;
using Jama.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Jama.Infrastructure.Documents;

/// <summary>
/// The Ministry of Interior storage calculation sheet.
///
/// This reproduces a submission document, so it is laid out to match rather than
/// to look pleasant: the Ministry's reviewers read a known sheet and a redesign
/// of it is a document that gets handed back. Column order, wording and the
/// colour coding all follow the reference.
///
/// The palette was read out of the reference PDF's own content streams rather
/// than sampled by eye, so these are its values and not approximations of them.
/// The colour coding carries meaning: red marks the two figures a reviewer
/// checks against each other — what the job REQUIRES and what the array makes
/// AVAILABLE — and green marks what is being PROPOSED for purchase.
///
/// Landscape, because 21 columns will not fit portrait at a legible size. The
/// reference is landscape for the same reason.
///
/// Every figure arrives already worked out. This class computes nothing, so the
/// sheet and the on-screen calculator cannot disagree.
/// </summary>
public sealed class MoiStoragePdfGenerator : IMoiStoragePdfGenerator
{
    private static readonly Color Maroon = Color.FromHex("#8A1538");
    private static readonly Color Navy = Color.FromHex("#0E233D");
    private static readonly Color Shade = Color.FromHex("#F2F2F2");
    private static readonly Color Rule = Color.FromHex("#000000");
    private static readonly Color Required = Color.FromHex("#C00000");
    private static readonly Color Proposed = Color.FromHex("#00B050");
    private static readonly Color White = Color.FromHex("#FFFFFF");

    // The letterhead rule beside the logo, in the brand's own two colours.
    private static readonly Color BrandOrange = Color.FromHex("#F6993D");
    private static readonly Color BrandBlue = Color.FromHex("#2594D2");

    // The sizing tool's own chrome, as the reference's screenshot shows it.
    private static readonly Color CalcHeader = Color.FromHex("#4A90D9");
    private static readonly Color CalcBorder = Color.FromHex("#C7D3DE");
    private static readonly Color CalcShade = Color.FromHex("#EEF3F8");

    private static readonly byte[] LogoBytes =
        LoadEmbedded("Jama.Infrastructure.Documents.Assets.jamago-logo.png");

    /// <summary>
    /// The company stamp, and the signature if it is scanned in with it.
    ///
    /// Optional on purpose: LoadEmbedded returns empty when the asset is absent,
    /// and the sheet then prints without it rather than failing or drawing a
    /// placeholder. Drop jamago-stamp.png into Documents/Assets and it appears —
    /// the csproj already includes it when present.
    /// </summary>
    private static readonly byte[] StampBytes =
        LoadEmbedded("Jama.Infrastructure.Documents.Assets.jamago-stamp.png");

    private static byte[] LoadEmbedded(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (stream is null) return [];
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static string N2(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

    /// <summary>Storage as the sheet writes it: 129.6 and 144, not 129.60 and
    /// 144.00. Trailing zeros are noise on a document someone reads across.</summary>
    private static string Tb(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string N0(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public byte[] Generate(MoiStoragePdfModel model)
    {
        return Document.Create(container =>
        {
            container.Page(page => ComposeSheet(page, model, model.Primary, snapshot: null));

            // The ANPR sheet is a second page on the same terms, omitted entirely
            // when the quotation has no number-plate cameras — the reference does
            // not print an empty one.
            if (model.Anpr is { } anpr)
                container.Page(page => ComposeSheet(page, model, anpr, model.Snapshot));
        }).GeneratePdf();
    }

    private static void ComposeSheet(
        PageDescriptor page,
        MoiStoragePdfModel model,
        MoiStorageSheet sheet,
        MoiSnapshotBlock? snapshot)
    {
        page.Size(PageSizes.A4.Landscape());
        page.Margin(22);
        page.DefaultTextStyle(x => x.FontSize(7).FontColor(Rule).FontFamily(Fonts.Calibri));

        page.Content().Column(column =>
        {
            column.Item().Element(ComposeLetterhead);
            column.Item().PaddingTop(12).Element(x => ComposeProjectBlock(x, model, sheet));
            column.Item().PaddingTop(10).Element(x => ComposeBanner(x, sheet.Title, Maroon, 9f));
            column.Item().Element(x => ComposeBanner(x, sheet.Subtitle, Maroon, 8f));
            column.Item().Element(x => ComposeTable(x, sheet));
            var which = sheet.Title.Contains("ANPR", StringComparison.OrdinalIgnoreCase)
                ? "Summary (ANPR Storage)"
                : "Summary (Primary Storage)";

            if (snapshot is { } snap)
            {
                // Side by side on the ANPR page. Stacked, the two blocks overflow
                // onto a third sheet, and a submission that runs to a mostly blank
                // page reads as a printing mistake. The reference sets them across
                // the width for the same reason.
                column.Item().PaddingTop(14).Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Element(x => ComposeBanner(x, which, Navy, 8.5f));
                        left.Item().Element(x => ComposeSummary(x, sheet));
                    });

                    row.ConstantItem(14);

                    row.RelativeItem().Column(right =>
                    {
                        right.Item().Element(x => ComposeBanner(
                            x, $"ANPR SNAPSHOT STORAGE FOR {snap.RecordingDays} DAYS", Navy, 8.5f));
                        right.Item().Element(x => ComposeSnapshot(x, snap));
                    });
                });
            }
            else
            {
                column.Item().PaddingTop(14).Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Element(x => ComposeBanner(x, which, Navy, 8.5f));
                        left.Item().Element(x => ComposeSummary(x, sheet));
                    });

                    row.ConstantItem(14);

                    // The manufacturer's sizing tool, beside the summary. On the
                    // reference this is a screenshot somebody pasted in; drawn
                    // from our own figures instead, it cannot fall out of step
                    // with the sheet it is meant to corroborate.
                    row.RelativeItem().Element(x => ComposeDiskCalculator(x, sheet));
                });
            }

            column.Item().Element(ComposeStamp);
        });
    }

    /// <summary>
    /// The company stamp at the foot, where the reference carries it.
    ///
    /// Draws nothing when no stamp is embedded. An empty box where a seal should
    /// be reads worse on a submission than no seal at all — it invites the reader
    /// to wonder what failed to print.
    /// </summary>
    private static void ComposeStamp(IContainer container)
    {
        if (StampBytes.Length == 0) return;

        container.PaddingTop(10).AlignLeft().Width(120).Image(StampBytes).FitWidth();
    }

    /// <summary>Logo and the rule beneath it — the company's own letterhead, which
    /// the reference carries on both pages.</summary>
    private static void ComposeLetterhead(IContainer container)
    {
        container.Row(row =>
        {
            if (LogoBytes.Length > 0)
                row.ConstantItem(150).AlignMiddle().Image(LogoBytes).FitWidth();
            else
                row.ConstantItem(150).Text("JAMA GO").FontSize(16).Bold().FontColor(Navy);

            // The brand's striped rule: three short orange dashes running into a
            // long blue bar. Drawn rather than shipped as artwork so it stretches
            // to whatever width is left beside the logo.
            row.RelativeItem().AlignMiddle().PaddingLeft(14).Height(9).Row(bar =>
            {
                for (var i = 0; i < 3; i++)
                {
                    bar.ConstantItem(11).Background(BrandOrange);
                    bar.ConstantItem(4);
                }

                bar.RelativeItem().Background(BrandBlue);
            });
        });
    }

    private static void ComposeProjectBlock(
        IContainer container,
        MoiStoragePdfModel model,
        MoiStorageSheet sheet)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(150);
                columns.RelativeColumn();
            });

            LabelCell(table.Cell(), "Project Name");
            ValueCell(table.Cell(), string.IsNullOrWhiteSpace(sheet.ProjectName) ? model.ProjectName : sheet.ProjectName);

            LabelCell(table.Cell(), "Revision No.");
            ValueCell(table.Cell(), model.RevisionNo);

            LabelCell(table.Cell(), "Date:");
            ValueCell(table.Cell(), model.Date.ToString("d/M/yyyy", CultureInfo.InvariantCulture));
        });
    }

    private static void LabelCell(IContainer cell, string text) =>
        cell.Border(1).BorderColor(Rule).Background(Maroon)
            .PaddingVertical(4).PaddingHorizontal(8).AlignCenter()
            .Text(text).FontSize(8).Bold().FontColor(White);

    private static void ValueCell(IContainer cell, string text) =>
        cell.Border(1).BorderColor(Rule)
            .PaddingVertical(4).PaddingHorizontal(8)
            .Text(text).FontSize(8).Bold().FontColor(Navy);

    private static void ComposeBanner(IContainer container, string text, Color background, float size) =>
        container.Border(1).BorderColor(Rule).Background(background)
            .PaddingVertical(4).AlignCenter()
            .Text(text).FontSize(size).Bold().FontColor(White).LetterSpacing(0.04f);

    /// <summary>
    /// The 21-column table. Widths are proportional rather than fixed so the whole
    /// sheet fits the page at a legible size; the reference's own columns vary the
    /// same way, wide for the headings that carry sentences and narrow for counts.
    /// </summary>
    private static void ComposeTable(IContainer container, MoiStorageSheet sheet)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2.6f);   // NVR / storage server
                columns.RelativeColumn(1.1f);   // channels
                columns.RelativeColumn(1.1f);   // hdd bays
                columns.RelativeColumn(1.0f);   // type
                columns.RelativeColumn(1.1f);   // size
                columns.RelativeColumn(1.0f);   // pool
                columns.RelativeColumn(1.3f);   // # hdd each array
                columns.RelativeColumn(1.0f);   // hdd for raid
                columns.RelativeColumn(1.1f);   // hot spare
                columns.RelativeColumn(1.0f);   // total hdd
                columns.RelativeColumn(1.1f);   // cameras
                columns.RelativeColumn(1.2f);   // resolution
                columns.RelativeColumn(1.1f);   // codec
                columns.RelativeColumn(0.8f);   // fps
                columns.RelativeColumn(1.1f);   // bitrate
                columns.RelativeColumn(1.0f);   // motion
                columns.RelativeColumn(1.1f);   // days
                columns.RelativeColumn(1.5f);   // per camera
                columns.RelativeColumn(1.4f);   // total required
                columns.RelativeColumn(1.3f);   // available
                columns.RelativeColumn(1.3f);   // proposed
            });

            table.Header(header =>
            {
                string[] headings =
                [
                    "NVR/ Storage server", "No. of Channels", "Number of HDD Bays", "Type of HDD",
                    "Size of HDD (TB)", "Disk Pool/Array", "# of HDD for Each Array", "HDD for RAID",
                    "Hot Spare HDD", "Total #HDD", "Number of Cameras", "Recording resolution",
                    "Recording Codec", "FPS", "Bitrate (Mbps)", "Motion (%)", "Recording days",
                    "Required storage for 1 camera (TB)", "Total storage required (TB)",
                    "Available storage (TB)", "Proposed Storage (TB)",
                ];

                foreach (var heading in headings)
                    header.Cell().Border(1).BorderColor(Rule).Background(White)
                        .PaddingVertical(3).PaddingHorizontal(2).AlignCenter().AlignMiddle()
                        .Text(heading).FontSize(5.6f).Bold().FontColor(Rule);
            });

            var rows = Math.Max(1, sheet.Arrays.Count);

            for (var i = 0; i < rows; i++)
            {
                var array = sheet.Arrays.Count > i ? sheet.Arrays[i] : null;

                // The columns that describe the server, not the pool, are stated
                // once and spanned down — as the sheet sets them.
                if (i == 0)
                {
                    Spanned(table, rows, sheet.RecorderLabel, bold: false);
                    Spanned(table, rows, sheet.Channels.ToString(CultureInfo.InvariantCulture));
                    Spanned(table, rows, sheet.HddBays.ToString(CultureInfo.InvariantCulture));
                    Spanned(table, rows, sheet.HddType);
                    Spanned(table, rows, N0(sheet.DiskTerabytes));
                }

                Body(table.Cell(), array?.Letter ?? "A");
                Body(table.Cell(), array?.DataDisks.ToString(CultureInfo.InvariantCulture) ?? "0");
                Body(table.Cell(), array?.ParityDisks.ToString(CultureInfo.InvariantCulture) ?? "0");

                if (i == 0)
                {
                    Spanned(table, rows, sheet.HotSpareDisks.ToString(CultureInfo.InvariantCulture));
                    Spanned(table, rows, sheet.TotalDisks.ToString(CultureInfo.InvariantCulture), bold: true);
                    Spanned(table, rows, sheet.Cameras.ToString(CultureInfo.InvariantCulture), bold: true);
                    Spanned(table, rows, sheet.Resolution);
                    Spanned(table, rows, sheet.Codec);
                    Spanned(table, rows, sheet.Fps.ToString(CultureInfo.InvariantCulture));
                    Spanned(table, rows, N2(sheet.BitrateMbps));
                    Spanned(table, rows, sheet.MotionPercent.ToString(CultureInfo.InvariantCulture));
                    Spanned(table, rows, sheet.RecordingDays.ToString(CultureInfo.InvariantCulture));
                    Spanned(table, rows, N2(sheet.PerCameraTerabytes));
                    Spanned(table, rows, N2(sheet.RequiredTerabytes), colour: Required, bold: true);
                }

                Body(table.Cell(), array is null ? "0" : Tb(array.AvailableTerabytes), Required, bold: true);
                Body(table.Cell(), array is null ? "0" : Tb(array.ProposedTerabytes), Proposed, bold: true);
            }

            // The grand-total band belongs to this table, not under it: its
            // figures have to fall beneath the columns they total, and a second
            // table with its own widths cannot guarantee that.
            table.Cell().ColumnSpan(5).Border(1).BorderColor(Rule).Background(Shade).MinHeight(16);
            BandLabel(table.Cell().ColumnSpan(4), "Grand Total:");
            Body(table.Cell(), sheet.TotalDisks.ToString(CultureInfo.InvariantCulture), bold: true);
            Body(table.Cell(), sheet.Cameras.ToString(CultureInfo.InvariantCulture), bold: true);
            table.Cell().ColumnSpan(6).Border(1).BorderColor(Rule).Background(Shade).MinHeight(16);
            BandLabel(table.Cell(), "Grand Total Storage:");
            Body(table.Cell(), Tb(sheet.RequiredTerabytes), Required, bold: true);
            Body(table.Cell(), Tb(sheet.AvailableTerabytes), Required, bold: true);
            Body(table.Cell(), Tb(sheet.ProposedTerabytes), Proposed, bold: true);
        });
    }

    private static void BandLabel(IContainer cell, string text) =>
        cell.Border(1).BorderColor(Rule).Background(Maroon)
            .PaddingVertical(4).PaddingHorizontal(5).AlignRight().AlignMiddle()
            .Text(text).FontSize(6.6f).Bold().FontColor(White);

    private static void Spanned(
        TableDescriptor table,
        int rows,
        string text,
        Color? colour = null,
        bool bold = true)
    {
        var span = table.Cell().RowSpan((uint)rows)
            .Border(1).BorderColor(Rule)
            .PaddingVertical(4).PaddingHorizontal(2)
            .AlignCenter().AlignMiddle()
            .Text(text).FontSize(6.4f).FontColor(colour ?? Rule);

        if (bold) span.Bold();
    }

    private static void Body(IContainer cell, string text, Color? colour = null, bool bold = false)
    {
        var span = cell.Border(1).BorderColor(Rule)
            .PaddingVertical(4).PaddingHorizontal(2)
            .AlignCenter().AlignMiddle()
            .Text(text).FontSize(6.4f).FontColor(colour ?? Rule);

        if (bold) span.Bold();
    }

    /// <summary>
    /// The Dahua Disk Calculator panel the reference carries as evidence: the
    /// inputs given to the manufacturer's tool and the capacity it returned.
    ///
    /// Bitrate is shown per channel in Kbps, and the conversion is binary —
    /// 2.5 Mbps is the tool's 2560 Kbps. That is not a rounding choice: the
    /// reference sheet prints 2.56 in its own Bitrate column, having divided by
    /// 1000, while every figure on it derives from the 2.5 that 2560/1024 gives.
    /// Following the tool keeps this panel agreeing with the table beside it.
    /// </summary>
    private static void ComposeDiskCalculator(IContainer container, MoiStorageSheet sheet)
    {
        var kbps = Math.Round(sheet.BitrateMbps * 1024m, 0, MidpointRounding.AwayFromZero);
        var aggregate = sheet.BitrateMbps * sheet.Cameras;

        container.Border(1).BorderColor(CalcBorder).Column(column =>
        {
            column.Item().Background(CalcHeader).PaddingVertical(4).PaddingHorizontal(8)
                .Text("Disk Calculator").FontSize(8.5f).Bold().FontColor(White);

            column.Item().Background(White).Padding(6).Column(body =>
            {
                body.Item().Table(grid =>
                {
                    grid.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(0.6f);
                        columns.RelativeColumn(1.1f);
                        columns.RelativeColumn(1.4f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.8f);
                    });

                    foreach (var heading in new[]
                             { "NO.", "Channels", "Compression", "Resolution", "FrameRate", "Bitrate/Ch(Kbps)" })
                        grid.Cell().Background(CalcShade).Border(1).BorderColor(CalcBorder)
                            .PaddingVertical(3).PaddingHorizontal(3).AlignCenter()
                            .Text(heading).FontSize(6).Bold().FontColor(Rule);

                    CalcCell(grid.Cell(), "1");
                    CalcCell(grid.Cell(), sheet.Cameras.ToString(CultureInfo.InvariantCulture));
                    CalcCell(grid.Cell(), sheet.Codec);
                    CalcCell(grid.Cell(), sheet.Resolution);
                    CalcCell(grid.Cell(), sheet.Fps.ToString(CultureInfo.InvariantCulture));
                    CalcCell(grid.Cell(), Tb(kbps));
                });

                body.Item().PaddingTop(4).Row(total =>
                {
                    total.RelativeItem().Text($"Total    {sheet.Cameras}")
                        .FontSize(6.5f).FontColor(Rule);
                    total.RelativeItem().AlignRight().Text($"{N2(aggregate)} Mbps")
                        .FontSize(6.5f).FontColor(Rule);
                });

                body.Item().PaddingTop(5).Row(tabs =>
                {
                    foreach (var (label, active) in new[]
                             { ("Disk Requirement", true), ("Recording day", false), ("RAID Calculator", false) })
                    {
                        var cell = tabs.RelativeItem()
                            .Border(1).BorderColor(CalcBorder)
                            .Background(active ? White : CalcShade)
                            .PaddingVertical(3).AlignCenter();

                        var text = cell.Text(label).FontSize(5.8f).FontColor(Rule);
                        if (active) text.Bold();
                    }
                });

                body.Item().PaddingTop(6).Row(footer =>
                {
                    footer.RelativeItem().AlignMiddle()
                        .Text($"Recording Days    {sheet.RecordingDays}")
                        .FontSize(6.5f).FontColor(Rule);

                    footer.RelativeItem().AlignRight().AlignMiddle().Text(text =>
                    {
                        text.Span("Request Capacity:  ").FontSize(6.5f).FontColor(Rule);
                        text.Span($"{Tb(sheet.RequiredTerabytes)}TB").FontSize(9).Bold().FontColor(Rule);
                    });
                });
            });
        });
    }

    private static void CalcCell(IContainer cell, string text) =>
        cell.Border(1).BorderColor(CalcBorder)
            .PaddingVertical(3).PaddingHorizontal(3).AlignCenter()
            .Text(text).FontSize(6).FontColor(Rule);

    private static void ComposeSummary(IContainer container, MoiStorageSheet sheet)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.RelativeColumn(1);
            });

            SummaryRow(table, "Number of Storage Server:", "1");
            SummaryRow(table, "Number of HDD:", sheet.TotalDisks.ToString(CultureInfo.InvariantCulture));
            SummaryRow(table, "Size of HDD:", $"{N0(sheet.DiskTerabytes)}TB");
            SummaryRow(table, "RAID Type:", sheet.RaidLevel.ToString(CultureInfo.InvariantCulture));
            SummaryRow(table, "Number of Cameras:", sheet.Cameras.ToString(CultureInfo.InvariantCulture));
            // The sheet states how many cameras record at the profile above, which
            // on a single-resolution job repeats the count and on a mixed one is
            // the only place the reader learns it is mixed.
            SummaryRow(table, $"{sheet.Resolution} Recording:", sheet.Cameras.ToString(CultureInfo.InvariantCulture));
            // Only when it is doing something. The Ministry's sheet has no such
            // row, so a compliant document must not grow one — but a factor above
            // 1 silently inflates every figure below, and that has to be visible
            // on the page rather than only in the calculator that made it.
            if (sheet.Redundancy > 1m)
                SummaryRow(table, "Video redundancy applied:", $"× {N0(sheet.Redundancy)}", Required);
            SummaryRow(table, "Total Required Storage:", Tb(sheet.RequiredTerabytes), Required);
            SummaryRow(table, "Total Available Storage:", Tb(sheet.AvailableTerabytes), Required);
            SummaryRow(table, "Total Proposed storage:", Tb(sheet.ProposedTerabytes), Proposed);

            // The ANPR page totals its snapshot disk rather than RAID and spares,
            // and words the row accordingly. Reusing the primary wording there
            // would describe a figure the page does not carry.
            var anprSheet = sheet.Title.Contains("ANPR", StringComparison.OrdinalIgnoreCase);
            SummaryRow(
                table,
                anprSheet
                    ? "Total Proposed storage Including ANPR Snapshot:"
                    : "Total Proposed storage Including Raid + Hotspare:",
                Tb(sheet.ProposedIncludingRaidTerabytes),
                Proposed);
        });
    }

    private static void ComposeSnapshot(IContainer container, MoiSnapshotBlock snap)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.RelativeColumn(1);
            });

            SummaryRow(table, "One camera snapshots per day (Approx)",
                snap.SnapshotsPerDay.ToString("N0", CultureInfo.InvariantCulture));
            SummaryRow(table, "One snapshot size (Kb/s)",
                snap.SnapshotKilobytes.ToString(CultureInfo.InvariantCulture));
            SummaryRow(table, "Recording days", snap.RecordingDays.ToString(CultureInfo.InvariantCulture));
            SummaryRow(table, "Per camera snapshots storage (TB)", Tb(snap.PerCameraTerabytes));
            SummaryRow(table, "No. of Cameras", snap.Cameras.ToString(CultureInfo.InvariantCulture));
            SummaryRow(table, "Size of HDD", $"{N0(snap.DiskTerabytes)}TB");
            SummaryRow(table, "Total Required Storage (TB)", Tb(snap.RequiredTerabytes), Required);
        });
    }

    private static void SummaryRow(TableDescriptor table, string label, string value, Color? colour = null)
    {
        table.Cell().Border(1).BorderColor(Rule).Background(Shade)
            .PaddingVertical(4).PaddingHorizontal(8)
            .Text(label).FontSize(7.5f).FontColor(Rule);

        table.Cell().Border(1).BorderColor(Rule)
            .PaddingVertical(4).PaddingHorizontal(8).AlignCenter()
            .Text(value).FontSize(7.5f).Bold().FontColor(colour ?? Navy);
    }
}
