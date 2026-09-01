using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Jama.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Jama.Infrastructure.Documents;

/// <summary>
/// Renders a bill of quantities as a branded PDF.
///
/// Shares the palette and embedded logo with the invoice and quotation
/// generators so everything a client receives looks like one company. The
/// layout is its own: a BOQ is read section by section, with a subtotal at the
/// foot of each, which neither of the others needs.
/// </summary>
public sealed class BoqPdfGenerator : IBoqPdfGenerator
{
    private static readonly Color Brand = Color.FromHex("#2594D2");
    private static readonly Color BrandStrong = Color.FromHex("#1A7BB5");
    private static readonly Color Accent = Color.FromHex("#C2660F");
    private static readonly Color Ink = Color.FromHex("#0F2846");
    private static readonly Color InkSoft = Color.FromHex("#3E5872");
    private static readonly Color Muted = Color.FromHex("#7C93A6");
    private static readonly Color PanelBg = Color.FromHex("#F2F8FC");
    private static readonly Color PanelBorder = Color.FromHex("#DCEAF4");
    private static readonly Color SectionBg = Color.FromHex("#EAF3FA");
    /// <summary>Alternate-row tint, deliberately part-transparent so the
    /// watermark beneath still reads through it.</summary>
    private static readonly Color RowAlt = Color.FromHex("#66F2F8FC");
    private static readonly Color Line = Color.FromHex("#E3EBF1");
    private static readonly Color White = Color.FromHex("#FFFFFF");

    private static readonly byte[] LogoBytes =
        LoadEmbedded("Jama.Infrastructure.Documents.Assets.jamago-logo.png");

    /// <summary>The tall quote lockup, pre-faded to the same ~10% the invoice
    /// watermark uses. QuestPDF has no opacity element, so the fade is baked into
    /// the asset rather than applied at render time.</summary>
    private static readonly byte[] WatermarkBytes =
        LoadEmbedded("Jama.Infrastructure.Documents.Assets.jamago-quote-watermark.png");

    /// <summary>Static QR to the website, printed in the top-right corner of both
    /// proposal pages. The target never varies, so it is a build-time asset rather
    /// than something generated per document.</summary>
    private static readonly byte[] QrBytes =
        LoadEmbedded("Jama.Infrastructure.Documents.Assets.jamago-qr.png");

    /// <summary>
    /// Maker's marks, keyed by every spelling that should resolve to one.
    ///
    /// Mirrors CAMERA_BRANDS in the Angular client, deliberately: the quotation
    /// on screen and the quotation in the client's hand have to show the same
    /// logo for the same brand. A brand with no artwork here simply prints its
    /// name, which is why the fallback is not an error.
    /// </summary>
    private static readonly Dictionary<string, byte[]> BrandMarks = BuildBrandMarks();

    private static Dictionary<string, byte[]> BuildBrandMarks()
    {
        var files = new (string File, string[] Aliases)[]
        {
            ("hikvision", ["hikvision", "hik"]),
            ("dahua", ["dahua", "dahua technology"]),
            ("uniview", ["uniview", "unv"]),
            ("tiandy", ["tiandy"]),
        };

        var marks = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var (file, aliases) in files)
        {
            var bytes = LoadEmbedded($"Jama.Infrastructure.Documents.Assets.brands.{file}.png");
            if (bytes.Length == 0) continue;

            foreach (var alias in aliases)
                marks[alias] = bytes;
        }

        return marks;
    }

    private static byte[]? BrandMark(string? brand) =>
        !string.IsNullOrWhiteSpace(brand) && BrandMarks.TryGetValue(brand.Trim(), out var bytes)
            ? bytes
            : null;

    private static byte[] LoadEmbedded(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (stream is null) return [];

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    /// <summary>How long a quotation stands. Two weeks, counted from the issue
    /// date — long enough for a client to decide, short enough that supplier
    /// prices have not moved underneath us.</summary>
    private const int ValidityDays = 14;

    // ===== Letterhead =====
    //
    // Mirrors the contact component on the public site. Both are typed out
    // rather than shared, which is a real duplication — but a client-facing
    // document must not start printing a blank address because a service was
    // not wired up, and there is no configuration source for these yet.

    private const string CompanyName = "Jama Go Security Equipment";
    private const string CompanyShort = "Jama Go";
    private const string CompanyTagline = "A trusted partner to customers and suppliers";
    private const string CompanyAddress =
        "Unit 41, Zone 56, Street 340, Building No. 349, Al Ain Complex, Salwa Road";
    private const string CompanyCity = "Doha – Qatar";
    // Listed one per line on the letterhead, the way a switchboard is printed,
    // rather than run together on one line separated by dots.
    private const string CompanyPhone1 = "+974 3064 4006";
    private const string CompanyPhone2 = "+974 3139 5879";
    private const string CompanyPhone3 = "+974 4001 3599";
    private const string CompanyEmail = "info@jamago.qa";
    private const string CompanyWebsite = "www.jamago.qa";

    /// <summary>One clause of the commercial offer. <paramref name="Body"/> is the
    /// paragraph beneath the heading, where there is one; the bullets follow it.</summary>
    private sealed record Term(string Heading, string? Body, string[] Bullets)
    {
        /// <summary>A line set bold on its own between the body and the list. The
        /// validity term is the one clause a client actually has to act on, so it
        /// is lifted out of the paragraph rather than buried in it.</summary>
        public string? Emphasis { get; init; }

        /// <summary>Numbers the list 1., 2., 3. instead of bulleting it. Only the
        /// validity clause does this: its points are referred to by number when a
        /// client queries a price.</summary>
        public bool Numbered { get; init; }
    }

    /// <summary>
    /// The standing commercial terms, printed ahead of the priced tables.
    ///
    /// Held here as data rather than laid out by hand so the wording is edited in
    /// one place and the numbering can never disagree with the order on the page.
    /// The validity clause reads <see cref="ValidityDays"/> for the same reason:
    /// the tables page already prints a "valid until" date, and a letter quoting a
    /// different term would contradict the document it introduces.
    /// </summary>
    private static readonly Term[] Terms =
    [
        new("Offer Validity and Price Escalation",
            "These prices are valid for a Contract or Purchase Order coming into force during " +
            "the validity of this offer.",
            [
                "Prices may be subject to revision after that validity date.",
                $"For any variation in quantity, {CompanyShort} reserves the right to revise the pricing and the delivery period.",
                "All unit prices are in QAR, inclusive of customs, transportation and any other charges within Qatar, and apply only to the quantities stated.",
            ])
        {
            Emphasis = $"The offer is valid for {ValidityDays} days from the date of this offer.",
            Numbered = true,
        },

        new("Terms of Payment", null,
            [
                "50% advance payment with the LPO, 40% upon delivery, and the remaining 10% upon completion.",
            ]),

        new("Delivery of Materials", null,
            [
                "Material delivery: within 3–6 days after official confirmation and advance payment.",
                "MOI documentation and approval process: 4–8 weeks.",
                "Project completion: within 8–12 weeks.",
            ]),

        new("Scope", null,
            [
                "Supply of the materials listed in the item descriptions.",
            ]),

        new("Out of Scope", null,
            [
                "Installation, electrical and fibre cabling, and copper cabling for the cameras.",
                "Both-side termination, labelling and camera installation, MOI documentation, and project sign-off.",
                "Anything not mentioned in the scope.",
            ]),

        new("Warranty", null,
            [
                "Cameras and NVR — 2 years.",
                "All other equipment — 1 year.",
                "Physical damage is not covered. We follow the manufacturer's standard warranty against manufacturing defects only.",
            ]),
    ];

    /// <summary>The two clauses that qualify everything above them, so they are set
    /// apart from the numbered list rather than buried as its last bullets.</summary>
    private const string WarrantyVoid =
        "THIS WARRANTY IS VOID IF the device has been damaged by negligence, mishandling, acts of " +
        "third parties, accident, fire, flood, lightning, power surges or outages, or other events, " +
        "or has not been operated in accordance with the operating and installation instructions.";

    private const string VariationNote =
        "Any variation in quantity or design suggested by the client or by MOI-SSD will be calculated " +
        "as a variation and invoiced separately.";

    /// <summary>Matches an email inside a display name, capturing only the part
    /// before the "@".</summary>
    private static readonly Regex EmbeddedEmail =
        new(@"<\s*([^@<>\s]+)@[^<>]*>", RegexOptions.Compiled);

    /// <summary>"Jane Doe &lt;jane@jamago.qa&gt;" becomes "Jane Doe (jane)". The
    /// domain is ours and identical on every quotation, so printing it on a
    /// client-facing document adds a line of noise and no information.</summary>
    private static string? PreparedBy(string? value) =>
        string.IsNullOrWhiteSpace(value) ? value : EmbeddedEmail.Replace(value, "($1)").Trim();

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

    private static string Date(DateOnly value) =>
        value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>"26th July 2026" — the long form the cover and the letter open
    /// with. The compact "26 Jul 2026" the tables use reads as a filing code,
    /// which is right above a column of figures and wrong under a signature.</summary>
    private static string LongDate(DateOnly value) =>
        $"{value.Day}{DayOrdinal(value.Day)} {value.ToString("MMMM yyyy", CultureInfo.InvariantCulture)}";

    /// <summary>11th, 12th and 13th break the last-digit rule and have to be
    /// matched before it.</summary>
    private static string DayOrdinal(int day) => day switch
    {
        11 or 12 or 13 => "th",
        _ when day % 10 == 1 => "st",
        _ when day % 10 == 2 => "nd",
        _ when day % 10 == 3 => "rd",
        _ => "th",
    };

    /// <summary>Trims a trailing ".00" so quantities read "12" not "12.00", while
    /// "2.5" keeps its half — cable is measured in metres.</summary>
    private static string Qty(decimal value) =>
        value == Math.Floor(value)
            ? ((long)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);

    public byte[] Generate(BoqPdfModel model)
    {
        return Document.Create(container =>
        {
            // Three page definitions, in the order a client reads them: the cover
            // that says who this is for, the letter that states the terms, then the
            // priced tables the letter introduces. Page numbering runs across all
            // three, so the tables open at "Page 3 of n" rather than restarting.
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Ink).FontFamily(Fonts.Calibri));

                // No watermark and no page number: a cover that numbers itself
                // reads as a form. The letterhead is part of the cover's own
                // column rather than a page footer, because it follows
                // "Prepared By:" and must sit directly beneath it.
                page.Content().Element(content => ComposeCover(content, model));
            });

            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Ink).FontFamily(Fonts.Calibri));

                page.Background().Element(ComposeWatermark);
                page.Header().Element(header => ComposeProposalHeader(header, model));
                page.Content().PaddingTop(14).Element(content => ComposeProposal(content, model));
                page.Footer().Element(footer => ComposeFooter(footer, model));
            });

            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Ink).FontFamily(Fonts.Calibri));

                page.Background().Element(ComposeWatermark);
                page.Header().Element(header => ComposeHeader(header, model));
                page.Content().PaddingTop(14).Element(content => ComposeContent(content, model));
                page.Footer().Element(footer => ComposeFooter(footer, model));
            });
        }).GeneratePdf();
    }

    // ===== Page 1: cover =====

    /// <summary>
    /// The mark and the QR, on the line every proposal page opens with.
    ///
    /// Shared by the cover and the letter so the two sit at identical heights —
    /// laid out separately they drifted by a few points, which reads as a
    /// printing fault rather than a design.
    /// </summary>
    private static void ComposeProposalCorners(IContainer container)
    {
        container.Row(row =>
        {
            if (LogoBytes.Length > 0)
                row.ConstantItem(140).AlignMiddle().Image(LogoBytes).FitWidth();
            else
                row.ConstantItem(140).AlignMiddle().Text(CompanyName)
                    .FontSize(12).Bold().FontColor(Brand);

            row.RelativeItem();

            if (QrBytes.Length > 0)
                row.ConstantItem(52).AlignTop().Image(QrBytes).FitWidth();
        });
    }

    private static void ComposeCover(IContainer container, BoqPdfModel model)
    {
        container.Column(column =>
        {
            column.Item().Element(ComposeProposalCorners);

            // Ref no stands alone above the title, centred, the way the trade
            // sets a proposal cover.
            column.Item().PaddingTop(52).AlignCenter().Text(text =>
            {
                text.Span("Ref No: ").FontSize(11).FontColor(InkSoft);
                text.Span(model.BoqNumber).FontSize(11).Bold().FontColor(Ink);
            });

            // Boxed rather than merely bold: the rule around it is what makes
            // this read as the document's title and not as another heading.
            column.Item().PaddingTop(26).AlignCenter().Border(1.2f).BorderColor(Ink)
                .PaddingVertical(9).PaddingHorizontal(34)
                .Text("COMMERCIAL PROPOSAL")
                .FontSize(14).Bold().FontColor(Ink);

            column.Item().PaddingTop(26).AlignCenter().Width(330)
                .Element(panel => ComposeCoverPanel(panel, model));

            column.Item().PaddingTop(44).Text("Prepared By:")
                .FontSize(10).Bold().Italic().Underline().FontColor(Accent);

            column.Item().PaddingTop(10).Element(ComposeCoverCompany);
        });
    }

    /// <summary>
    /// Prepared For / Project Title / Date of Submission, stacked and centred on
    /// a tinted panel, each pair split by a rule.
    ///
    /// Label above value rather than beside it: the three values are of very
    /// different lengths, and a label column wide enough for "Date of Submission"
    /// left the short ones stranded against a column of white.
    /// </summary>
    private static void ComposeCoverPanel(IContainer container, BoqPdfModel model)
    {
        container.Background(PanelBg).PaddingVertical(16).PaddingHorizontal(18).Column(column =>
        {
            CoverPair(column, "Prepared For:", CoverClient(model));

            column.Item().PaddingVertical(11).Height(1).Background(Accent);

            CoverPair(column, "Project Title:", model.ProjectName.ToUpperInvariant());

            column.Item().PaddingVertical(11).Height(1).Background(Accent);

            column.Item().AlignCenter().Text(text =>
            {
                text.Span("Date of Submission: ").FontSize(9.5f).FontColor(InkSoft);
                text.Span(LongDate(model.IssueDate)).FontSize(9.5f).Bold().FontColor(Ink);
            });
        });
    }

    private static void CoverPair(ColumnDescriptor column, string label, string value)
    {
        column.Item().AlignCenter().Text(label).FontSize(9.5f).FontColor(InkSoft);
        column.Item().PaddingTop(4).AlignCenter().Text(value)
            .FontSize(11.5f).Bold().FontColor(Ink);
    }

    /// <summary>The client and where they are, on one line — "Bahzat Group, Doha
    /// – Qatar". The location is dropped rather than printed empty when a BOQ
    /// never recorded one.</summary>
    private static string CoverClient(BoqPdfModel model)
    {
        var parts = new[] { model.ClientName, model.SiteLocation }
            .Where(part => !string.IsNullOrWhiteSpace(part));

        var joined = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(joined) ? "—" : joined;
    }

    /// <summary>The mark beside the contact block, as a letterhead sets it — the
    /// logo carries the identity and the lines beside it carry the detail, rather
    /// than the name being repeated as text next to its own logo.</summary>
    private static void ComposeCoverCompany(IContainer container)
    {
        container.Row(row =>
        {
            if (LogoBytes.Length > 0)
                row.ConstantItem(120).AlignTop().PaddingTop(2).Image(LogoBytes).FitWidth();

            row.ConstantItem(14);

            row.RelativeItem().Column(column =>
            {
                column.Item().Text(CompanyName)
                    .FontSize(12).Bold().FontColor(BrandStrong);
                column.Item().PaddingTop(1).Text(CompanyTagline)
                    .FontSize(8.5f).Italic().FontColor(Accent);

                column.Item().PaddingTop(7).Text(CompanyAddress)
                    .FontSize(8.5f).FontColor(InkSoft);
                column.Item().Text(CompanyCity)
                    .FontSize(8.5f).FontColor(InkSoft);

                column.Item().PaddingTop(6);
                ContactRow(column, "Telephone", CompanyPhone1, false);
                ContactRow(column, "", CompanyPhone2, false);
                ContactRow(column, "", CompanyPhone3, false);
                ContactRow(column, "Email", CompanyEmail, true);
                ContactRow(column, "Website", CompanyWebsite, true);
            });
        });
    }

    /// <summary>One "Label : value" line, colons aligned down a fixed track so the
    /// values form a column. An empty label continues the row above it — the
    /// second and third phone numbers belong to "Telephone" and repeating the word
    /// three times would say nothing.</summary>
    private static void ContactRow(ColumnDescriptor column, string label, string value, bool link)
    {
        column.Item().PaddingTop(1).Row(row =>
        {
            row.ConstantItem(58).Text(label).FontSize(8.5f).FontColor(InkSoft);
            row.ConstantItem(8).Text(label.Length > 0 ? ":" : "").FontSize(8.5f).FontColor(InkSoft);

            var text = row.RelativeItem().Text(value).FontSize(8.5f);
            if (link) text.Bold().FontColor(Brand).Underline();
            else text.FontColor(Ink);
        });
    }

    // ===== Page 2: the commercial offer =====

    /// <summary>The same mark-and-QR line the cover opens with, so the two pages
    /// read as one letterhead. The ref sits under it rather than beside the logo,
    /// which is where the QR now is.</summary>
    private static void ComposeProposalHeader(IContainer container, BoqPdfModel model)
    {
        container.Column(column =>
        {
            column.Item().Element(ComposeProposalCorners);

            column.Item().PaddingTop(6).Text(text =>
            {
                text.Span("Ref No: ").FontSize(9).FontColor(InkSoft);
                text.Span(model.BoqNumber).FontSize(9).Bold().FontColor(Ink);
            });

            column.Item().PaddingTop(8).Height(2).Background(Brand);
        });
    }

    private static void ComposeProposal(IContainer container, BoqPdfModel model)
    {
        container.Column(column =>
        {
            column.Item().AlignRight().Text(LongDate(model.IssueDate))
                .FontSize(9).FontColor(InkSoft);

            // Addressee block, in the position a letter puts it.
            column.Item().PaddingTop(10).Text(CoverClient(model))
                .FontSize(10.5f).Bold().FontColor(Ink);
            column.Item().Text("Procurement Department").FontSize(9).FontColor(InkSoft);

            column.Item().PaddingTop(12).Element(subject => ComposeSubject(subject, model));

            column.Item().PaddingTop(12).Text("Dear Sir / Madam,")
                .FontSize(9.5f).Bold().FontColor(Ink);

            column.Item().PaddingTop(7).Text(
                    "With reference to the above subject, and to the requirements stated in your " +
                    "request, we are pleased to submit our priced commercial offer with the scope " +
                    "of work set out below.")
                .FontSize(9).FontColor(InkSoft).LineHeight(1.35f);

            var number = 0;
            foreach (var term in Terms)
                column.Item().PaddingTop(10).Element(x => ComposeTerm(x, ++number, term));

            column.Item().PaddingTop(12).Border(1).BorderColor(PanelBorder).Padding(9).Column(inner =>
            {
                inner.Item().Text(WarrantyVoid)
                    .FontSize(7.8f).FontColor(Muted).LineHeight(1.35f);
                inner.Item().PaddingTop(6).Text(VariationNote)
                    .FontSize(7.8f).FontColor(Muted).LineHeight(1.35f);
            });

            column.Item().PaddingTop(14).Text("Yours faithfully,")
                .FontSize(9).FontColor(InkSoft);
            column.Item().PaddingTop(3).Text(PreparedBy(model.PreparedByName) ?? CompanyName)
                .FontSize(9.5f).Bold().FontColor(Ink);
            column.Item().Text(CompanyName).FontSize(8.5f).FontColor(Muted);
        });
    }

    /// <summary>Project title and subject, aligned on their colons the way the
    /// rest of the trade writes them.</summary>
    private static void ComposeSubject(IContainer container, BoqPdfModel model)
    {
        container.Column(column =>
        {
            SubjectRow(column, "Project Title", model.ProjectName);
            SubjectRow(column, "Subject", "Priced / Commercial Offer");
        });
    }

    private static void SubjectRow(ColumnDescriptor column, string label, string value)
    {
        column.Item().PaddingTop(2).Row(row =>
        {
            row.ConstantItem(86).Text(label).FontSize(9).FontColor(Muted);
            row.ConstantItem(10).Text(":").FontSize(9).FontColor(Muted);
            row.RelativeItem().Text(value).FontSize(9).Bold().FontColor(Ink);
        });
    }

    private static void ComposeTerm(IContainer container, int number, Term term)
    {
        container.Column(column =>
        {
            column.Item().Text($"{number}.  {term.Heading}")
                .FontSize(9.5f).Bold().FontColor(BrandStrong);

            if (!string.IsNullOrWhiteSpace(term.Body))
                column.Item().PaddingTop(3).PaddingLeft(16).Text(term.Body)
                    .FontSize(8.6f).FontColor(InkSoft).LineHeight(1.35f);

            if (!string.IsNullOrWhiteSpace(term.Emphasis))
                column.Item().PaddingTop(5).PaddingLeft(16).Text(term.Emphasis)
                    .FontSize(8.8f).Bold().FontColor(Ink).LineHeight(1.35f);

            var item = 0;
            foreach (var bullet in term.Bullets)
            {
                var marker = term.Numbered ? $"{++item}." : "•";

                column.Item().PaddingTop(3).PaddingLeft(term.Numbered ? 28 : 16).Row(row =>
                {
                    // Marker in its own track, so a line that wraps aligns under
                    // its own text rather than back under the dot.
                    row.ConstantItem(14).Text(marker).FontSize(8.6f)
                        .FontColor(term.Numbered ? InkSoft : Accent);
                    row.RelativeItem().Text(bullet)
                        .FontSize(8.6f).FontColor(InkSoft).LineHeight(1.35f);
                });
            }
        });
    }

    private static void ComposeHeader(IContainer container, BoqPdfModel model)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                if (LogoBytes.Length > 0)
                    row.ConstantItem(150).AlignMiddle().Image(LogoBytes).FitWidth();
                else
                    row.RelativeItem().Text("JAMA GO").FontSize(20).Bold().FontColor(Brand);

                row.RelativeItem().AlignRight().Column(right =>
                {
                    right.Item().Text("QUOTATION")
                        .FontSize(24).Bold().FontColor(BrandStrong);
                    // No status line: a quote handed to a client is the
                    // offer itself. "DRAFT" is our workflow, not their concern.
                    right.Item().PaddingTop(3).Text(text =>
                    {
                        text.Span("Quotation no  ").FontSize(9).FontColor(Muted);
                        text.Span(model.BoqNumber).FontSize(12).Bold().FontColor(Ink);
                    });
                });
            });

            column.Item().PaddingTop(10).Height(3).Background(Brand);
        });
    }

    private static void ComposeContent(IContainer container, BoqPdfModel model)
    {
        container.Column(column =>
        {
            column.Item().Element(x => ComposeDetails(x, model));

            foreach (var section in model.Sections)
                column.Item().PaddingTop(12).Element(x => ComposeSection(x, section));

            column.Item().PaddingTop(14).Element(x => ComposeTotal(x, model));

            if (!string.IsNullOrWhiteSpace(model.Notes))
                column.Item().PaddingTop(12).Element(x => ComposeNotes(x, model.Notes!));
        });
    }

    /// <summary>
    /// Who the quote is for and what it covers, as a full-width table.
    ///
    /// Two label/value pairs per row rather than one column down the right: six
    /// short values stacked in half the page left the other half blank and forced
    /// the longest of them to wrap. Client facts run down the left, document
    /// facts down the right.
    ///
    /// It sits in the content rather than the page header so it prints once. On a
    /// quote that runs to a second page, repeating the client's phone number
    /// above every table costs a third of the page and tells nobody anything.
    /// </summary>
    private static void ComposeDetails(IContainer container, BoqPdfModel model)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(94);   // label
                columns.RelativeColumn(1);    // value
                columns.ConstantColumn(94);   // label
                // Slightly wider: "Prepared by" carries a name and an email.
                columns.RelativeColumn(1.15f);
            });

            DetailPair(table, "To", model.ClientName, "Date", Date(model.IssueDate));
            DetailPair(table, "Project", model.ProjectName, "Location", model.SiteLocation);
            DetailPair(table, "Contact number", model.ContactNumber, "Prepared by", PreparedBy(model.PreparedByName));

            // Spans the remaining three columns: the date alone means nothing
            // without the term beside it, and the two must not be split apart.
            DetailLabel(table.Cell(), "Valid until");
            DetailCell(table.Cell().ColumnSpan(3)).Text(text =>
            {
                text.Span(Date(model.IssueDate.AddDays(ValidityDays)))
                    .FontSize(9).Bold().FontColor(Accent);
                text.Span($"     Valid for {ValidityDays} days (2 weeks) from the date above.")
                    .FontSize(8).FontColor(Muted);
            });
        });
    }

    private static void DetailPair(
        TableDescriptor table,
        string leftLabel,
        string? leftValue,
        string rightLabel,
        string? rightValue)
    {
        DetailLabel(table.Cell(), leftLabel);
        DetailValue(table.Cell(), leftValue);
        DetailLabel(table.Cell(), rightLabel);
        DetailValue(table.Cell(), rightValue);
    }

    /// <summary>Background BEFORE the cell chain: applied after the padding it
    /// tints only the text box, leaving a short bar floating in the cell.</summary>
    private static void DetailLabel(IContainer container, string label) =>
        DetailCell(container.Background(PanelBg))
            .Text(label.ToUpperInvariant())
            .FontSize(6.8f).Bold().FontColor(Muted).LetterSpacing(0.08f);

    /// <summary>An empty value still prints its label with a dash, so a missing
    /// contact number reads as "not given" rather than as a field the document
    /// never had. No fill: the watermark shows through these cells.</summary>
    private static void DetailValue(IContainer container, string? value) =>
        DetailCell(container)
            .Text(string.IsNullOrWhiteSpace(value) ? "\u2014" : value)
            .FontSize(9).Bold().FontColor(Ink);

    private static IContainer DetailCell(IContainer container) =>
        container.Border(1).BorderColor(PanelBorder)
            .PaddingVertical(6).PaddingHorizontal(9)
            .AlignMiddle();

    private static void ComposeSection(IContainer container, BoqPdfSection section)
    {
        container.Column(column =>
        {
            // Section heading doubles as the table's caption, so a section that
            // breaks across pages still says what it is.
            // Three tracks with matching outer widths, so the title sits dead
            // centre of the band however long the subtotal beside it runs. A
            // centred RelativeItem would be pushed off by the subtotal's width.
            column.Item().Background(SectionBg).Padding(7)
                .AlignCenter()
                .Text($"{section.Number}.  {section.Title}")
                .FontSize(10).Bold().FontColor(BrandStrong);

            column.Item().Table(table =>
            {
                // The client's own layout: image, brand, model and description
                // each get a column instead of being stacked into one cell.
                // Page is A4 less 28pt margins = 539pt, so the fixed columns
                // total 406 and description takes what is left.
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);   // s.no
                    columns.ConstantColumn(54);   // brand
                    columns.ConstantColumn(46);   // image
                    columns.ConstantColumn(44);   // type
                    columns.ConstantColumn(66);   // model
                    columns.RelativeColumn(1);    // description
                    columns.ConstantColumn(32);   // unit
                    columns.ConstantColumn(28);   // qty
                    columns.ConstantColumn(52);   // unit price
                    columns.ConstantColumn(62);   // total
                });

                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "S.No", false);
                    HeaderCell(header.Cell(), "Brand", false);
                    HeaderCell(header.Cell(), "Image", false);
                    HeaderCell(header.Cell(), "Type", false);
                    HeaderCell(header.Cell(), "Model", false);
                    HeaderCell(header.Cell(), "Description", false);
                    HeaderCell(header.Cell(), "Unit", true);
                    HeaderCell(header.Cell(), "Qty", true);
                    HeaderCell(header.Cell(), "Unit Price", true);
                    HeaderCell(header.Cell(), "Total Amount", true);
                });

                var index = 0;
                foreach (var line in section.Lines)
                {
                    // Zebra striping so a long section stays readable across a row.
                    // Odd rows only: an opaque white fill on the rest would blank
                    // out the watermark behind the table.
                    var shaded = index++ % 2 == 1;

                    Body(table.Cell(), shaded).Text(line.Number).FontColor(Muted);

                    // Fixed height whether or not there is a picture, so rows in
                    // the same section line up rather than jumping about.
                    // The maker's mark where there is one, the name where there
                    // is not — most of a real catalogue is cable and brackets
                    // from suppliers whose logo nobody has on file.
                    var brandCell = Body(table.Cell(), shaded).AlignMiddle();
                    if (BrandMark(line.Brand) is { } mark)
                        // FitArea, not FitHeight: a wide wordmark scaled to a
                        // fixed height overflows a 54pt column, which QuestPDF
                        // reports as conflicting size constraints rather than
                        // clipping. Bounded both ways, it just gets smaller.
                        brandCell.Height(18).AlignLeft().Image(mark).FitArea();
                    else
                        brandCell.Text(Dash(line.Brand)).FontColor(InkSoft);

                    // Fixed box, as the editor has: a source photo may be
                    // 4000px wide or 200px tall and every row must still show
                    // the same size picture. FitArea contains it inside the box
                    // rather than cropping the camera out of its own thumbnail.
                    var photo = Body(table.Cell(), shaded).Height(38).AlignMiddle();
                    if (line.Image is { Length: > 0 })
                        photo.AlignCenter().Image(line.Image).FitArea();
                    else
                        photo.AlignCenter().Text(EmDash).FontSize(9).FontColor(Muted);
                    // Type before model, the order a specifier chooses them in:
                    // a brand, then the form factor, then the model that is both.
                    Body(table.Cell(), shaded).Text(Dash(line.Type)).FontSize(8).FontColor(InkSoft);
                    Body(table.Cell(), shaded).Text(Dash(line.ModelNo)).FontSize(8).FontColor(InkSoft);

                    Body(table.Cell(), shaded).Column(cell =>
                    {
                        cell.Item().Text(line.ItemName).Bold().FontColor(Ink);

                        if (!string.IsNullOrWhiteSpace(line.Description))
                            cell.Item().PaddingTop(1).Text(line.Description!)
                                .FontSize(7.5f).FontColor(Muted);

                        // Right-to-left on the cell, not the string: Arabic set in
                        // a left-to-right flow puts its punctuation on the wrong
                        // end of the line.
                        if (!string.IsNullOrWhiteSpace(line.DescriptionAr))
                            cell.Item().PaddingTop(1)
                                .ContentFromRightToLeft()
                                .Text(line.DescriptionAr!)
                                .FontSize(7.5f).FontColor(Muted);
                    });

                    Body(table.Cell(), shaded).AlignRight().Text(Unit(line.Uom)).FontColor(InkSoft);
                    Body(table.Cell(), shaded).AlignRight().Text(Qty(line.Quantity));
                    Body(table.Cell(), shaded).AlignRight().Text(Money(line.UnitRate));
                    Body(table.Cell(), shaded).AlignRight().Text(Money(line.LineTotal)).Bold();
                }

                // The section's money under Total Amount, where a reader adding
                // the rows up arrives at it — not beside the heading.
                table.Cell().ColumnSpan(8)
                    .BorderTop(1).BorderColor(PanelBorder)
                    .PaddingVertical(6).PaddingHorizontal(5)
                    .AlignRight()
                    .Text("Section total")
                    .FontSize(7.5f).Bold().FontColor(Muted).LetterSpacing(0.06f);

                table.Cell()
                    .BorderTop(1).BorderColor(PanelBorder)
                    .PaddingVertical(6).PaddingHorizontal(5)
                    .AlignRight()
                    .Text(Money(section.Subtotal))
                    .FontSize(9.5f).Bold().FontColor(BrandStrong);
            });
        });
    }

    private const string EmDash = "\u2014";

    /// <summary>
    /// How a unit is written on the document.
    ///
    /// The enum member stays "Piece" — it is the stored value, and renaming it
    /// would need a data migration — so only the printed form changes.
    /// </summary>
    private static string Unit(string uom) =>
        string.Equals(uom, "Piece", StringComparison.OrdinalIgnoreCase) ? "pcs" : uom;

    /// <summary>An em dash for an empty cell, so a blank reads as "not recorded"
    /// rather than as a column that failed to print.</summary>
    private static string Dash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? EmDash : value;

    private static void HeaderCell(IContainer container, string text, bool alignRight)
    {
        var cell = container.Background(BrandStrong).PaddingVertical(5).PaddingHorizontal(5);
        var styled = alignRight ? cell.AlignRight() : cell;
        styled.Text(text).FontSize(7).Bold().FontColor(White);
    }

    private static IContainer Body(IContainer container, bool shaded) =>
        (shaded ? container.Background(RowAlt) : container)
            .BorderBottom(1).BorderColor(Line)
            .PaddingVertical(4).PaddingHorizontal(5);

    private static void ComposeTotal(IContainer container, BoqPdfModel model)
    {
        container.Row(row =>
        {
            // Left half stays empty: the total belongs on the same side as the
            // amounts column it sums.
            row.RelativeItem();

            row.ConstantItem(260).Background(BrandStrong).Padding(9).Row(inner =>
            {
                inner.RelativeItem().Text("TOTAL (QAR)")
                    .FontSize(10).Bold().FontColor(White);
                inner.ConstantItem(110).AlignRight().Text(Money(model.Total))
                    .FontSize(13).Bold().FontColor(White);
            });
        });
    }

    private static void ComposeNotes(IContainer container, string body)
    {
        container.Border(1).BorderColor(PanelBorder).Padding(9).Column(column =>
        {
            column.Item().PaddingBottom(3).Text("NOTES")
                .FontSize(7.5f).Bold().FontColor(Brand).LetterSpacing(0.09f);
            column.Item().Text(body).FontColor(InkSoft);
        });
    }

    /// <summary>The logo, centred on every page, on the BACKGROUND layer — it sits
    /// under the content, not over it. That only works because the table rows no
    /// longer paint an opaque fill (see <see cref="Body"/>); the solid bands that
    /// remain — section headings, column headers, the total — hide it where they
    /// fall, which is what being behind something means. The layer does not
    /// displace content: the tables lay out exactly as they would without it.</summary>
    private static void ComposeWatermark(IContainer container)
    {
        if (WatermarkBytes.Length == 0) return;

        // Sized by width, not height: this lockup is portrait (roughly 1:1.56),
        // so 300pt across lands it about 470pt tall — centred on A4 with room
        // to spare at the head and foot.
        container.AlignCenter().AlignMiddle().Width(300).Image(WatermarkBytes).FitWidth();
    }

    private static void ComposeFooter(IContainer container, BoqPdfModel model)
    {
        container.Column(column =>
        {
            column.Item().PaddingBottom(5).Height(1).Background(Line);
            column.Item().Row(row =>
            {
                row.RelativeItem().Text($"Jama Go Security Equipment · Doha, Qatar · {model.BoqNumber}")
                    .FontSize(7.5f).FontColor(Muted);

                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Muted));
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        });
    }
}
