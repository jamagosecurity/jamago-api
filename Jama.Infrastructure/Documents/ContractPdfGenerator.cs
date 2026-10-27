using System.Reflection;
using System.Text.RegularExpressions;
using Jama.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Jama.Infrastructure.Documents;

/// <summary>
/// The signed contract a client receives once their quotation is approved —
/// the company's full Arabic supply-and-installation contract (ten numbered
/// "البند" clauses: references, scope, payment schedule, duration, warranty,
/// changes, cancellation, liability, jurisdiction, copies), not the shorter
/// bilingual bullet summary this document used to be.
///
/// Renders whatever <see cref="ContractPdfModel.Body"/> holds — staff's own
/// edited wording, or IContractTextTemplate's default — as flowing RTL text,
/// with one convention carried over from ComposeFreeformTerms in
/// BoqPdfGenerator: a line starting "البند" is a clause heading, a blank line
/// is a paragraph break, everything else is a plain paragraph. There is no
/// further structure to trust once someone has been free to edit the text.
/// Pages are not fixed — QuestPDF paginates the body automatically — except
/// for the signature block, appended fixed at the end.
/// </summary>
public sealed class ContractPdfGenerator : IContractPdfGenerator
{
    private static readonly Color Ink = Color.FromHex("#0F2846");
    private static readonly Color InkSoft = Color.FromHex("#3E5872");
    private static readonly Color Muted = Color.FromHex("#7C93A6");
    private static readonly Color Line = Color.FromHex("#E3EBF1");
    private static readonly Color White = Color.FromHex("#FFFFFF");
    private static readonly Color FooterBg = Color.FromHex("#0F2846");
    private static readonly Color Brand = Color.FromHex("#2594D2");
    private static readonly Color Accent = Color.FromHex("#F6993D");

    private const string CompanyName = "JAMA GO SECURITY EQUIPMENTS";
    private const string CompanyNameAr = "شركة جاما جو للمعدات الأمنية";
    private const string CompanyPhone1 = "+974 4001 3599";
    private const string CompanyPhone2 = "+974 3064 4006";
    private const string CompanyEmail = "info@jamago.com";
    private const string CompanyAddress =
        "Showroom, Unit 41, Building 349, St 340, Zone 56, Al Ain Complex, Salwa Road";

    private static readonly byte[] LogoBytes =
        LoadEmbedded("Jama.Infrastructure.Documents.Assets.jamago-logo.png");

    private static byte[] LoadEmbedded(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (stream is null) return [];
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    /// <summary>A line starting "البند" — a clause heading, same convention as
    /// NumberedHeading in BoqPdfGenerator.</summary>
    private static readonly Regex ClauseHeading = new(@"^البند\s+\S", RegexOptions.Compiled);

    /// <summary>A line starting "- " — the payment schedule's own convention
    /// in IContractTextTemplate.BuildDefault, rendered as a bullet rather
    /// than a plain paragraph.</summary>
    private static readonly Regex BulletLine = new(@"^-\s+", RegexOptions.Compiled);

    public byte[] Generate(ContractPdfModel model)
    {
        DocumentFonts.EnsureRegistered();

        var paragraphs = SplitParagraphs(model.Body);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor(Ink).FontFamily(DocumentFonts.Arabic));

                page.Header().Element(header => ComposeHeader(header, model));
                page.Content().PaddingTop(10).Column(content =>
                {
                    var first = true;
                    foreach (var paragraph in paragraphs)
                    {
                        // The title and the quoted system description open the
                        // document centered — everything after reads as the
                        // body of the agreement, right-aligned like the rest.
                        if (first)
                        {
                            content.Item().AlignCenter().Text(paragraph)
                                .FontSize(13).Bold().FontColor(Ink);
                            first = false;
                            continue;
                        }

                        content.Item().PaddingTop(10).Element(x => ComposeParagraph(x, paragraph));
                    }

                    content.Item().PaddingTop(28).Element(x => ComposeSignatures(x));
                });
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    // ===== Letterhead =====

    private static void ComposeHeader(IContainer container, ContractPdfModel model)
    {
        container.Row(row =>
        {
            if (LogoBytes.Length > 0)
                row.ConstantItem(140).AlignMiddle().Image(LogoBytes).FitWidth();
            else
                row.ConstantItem(140).AlignMiddle().Text(CompanyName)
                    .FontSize(11).Bold().FontColor(Brand);

            row.RelativeItem();

            row.ConstantItem(170).Border(1).BorderColor(Line).Padding(6).Column(col =>
            {
                col.Item().ContentFromRightToLeft().AlignRight()
                    .Text($"رقم العقد / Contract # : {model.ContractNumber}")
                    .FontFamily(DocumentFonts.Arabic).FontSize(8).FontColor(InkSoft);
                col.Item().ContentFromRightToLeft().AlignRight()
                    .Text($"المراجعة / Revision : {model.RevisionNumber}")
                    .FontFamily(DocumentFonts.Arabic).FontSize(8).FontColor(InkSoft);
                col.Item().ContentFromRightToLeft().AlignRight()
                    .Text($"التاريخ / Date : {model.ApprovedAt:dd/MM/yyyy}")
                    .FontFamily(DocumentFonts.Arabic).FontSize(8).FontColor(InkSoft);
            });
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Background(FooterBg).PaddingVertical(8).PaddingHorizontal(16).Row(row =>
        {
            row.RelativeItem().Text($"{CompanyPhone1}   {CompanyPhone2}")
                .FontSize(7.5f).FontColor(White);
            row.RelativeItem().AlignCenter().Text(CompanyEmail).FontSize(7.5f).FontColor(White);
            row.RelativeItem().AlignRight().Text(CompanyAddress).FontSize(7).FontColor(White);
        });
    }

    // ===== Body =====

    /// <summary>Blank-line-separated paragraphs, each with its own internal
    /// line breaks preserved — a clause heading and the lines under it travel
    /// together as one paragraph, the same grouping <see cref="Generate"/>
    /// expects from <see cref="IContractTextTemplate.BuildDefault"/>.</summary>
    private static List<string> SplitParagraphs(string body) =>
        body.Replace("\r\n", "\n")
            .Split("\n\n")
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

    private static void ComposeParagraph(IContainer container, string paragraph)
    {
        var lines = paragraph.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (lines.Count == 0) return;

        container.Column(column =>
        {
            foreach (var line in lines)
            {
                if (ClauseHeading.IsMatch(line))
                {
                    // A heading carries its own rule underneath, the same way
                    // ComposeSection in BoqPdfGenerator marks a new section —
                    // the one visual cue that says "new clause starts here"
                    // without staff having to add spacing by hand.
                    column.Item().PaddingTop(6).ContentFromRightToLeft().AlignRight()
                        .Text(line).FontFamily(DocumentFonts.Arabic).FontSize(11)
                        .Bold().FontColor(Brand);
                    column.Item().PaddingTop(2).Height(1).Background(Line);
                    continue;
                }

                if (BulletLine.IsMatch(line))
                {
                    var bulletText = BulletLine.Replace(line, "").Trim();
                    column.Item().PaddingTop(3).ContentFromRightToLeft().Row(row =>
                    {
                        row.RelativeItem().AlignRight().Text(bulletText)
                            .FontFamily(DocumentFonts.Arabic).FontSize(9.5f)
                            .FontColor(InkSoft).LineHeight(1.5f);
                        row.ConstantItem(12).AlignRight().Text("•")
                            .FontSize(9.5f).FontColor(Accent);
                    });
                    continue;
                }

                column.Item().PaddingTop(3).ContentFromRightToLeft().AlignRight()
                    .Text(line).FontFamily(DocumentFonts.Arabic).FontSize(9.5f)
                    .FontColor(InkSoft).LineHeight(1.55f);
            }
        });
    }

    // ===== Signatures =====

    private static void ComposeSignatures(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text("توقيعات الأطراف")
                .FontFamily(DocumentFonts.Arabic).FontSize(11).Bold().FontColor(Ink);

            // The second party sits left and the first sits right — RTL
            // convention places whichever is named first (الطرف الأول) on
            // the right, the same side the body text itself reads from.
            col.Item().PaddingTop(30).Row(row =>
            {
                row.RelativeItem().Column(inner =>
                {
                    inner.Item().AlignCenter().Text("الطرف الثاني (مالك العقار)")
                        .FontFamily(DocumentFonts.Arabic).FontSize(9.5f).Bold().FontColor(InkSoft);
                    inner.Item().PaddingTop(45).AlignCenter().Width(160)
                        .BorderBottom(1).BorderColor(Line);
                });

                row.RelativeItem().Column(inner =>
                {
                    inner.Item().AlignCenter().Text("الطرف الأول (المقاول)")
                        .FontFamily(DocumentFonts.Arabic).FontSize(9.5f).Bold().FontColor(InkSoft);
                    inner.Item().AlignCenter().Text(CompanyNameAr)
                        .FontFamily(DocumentFonts.Arabic).FontSize(9).FontColor(InkSoft);
                    inner.Item().PaddingTop(45).AlignCenter().Width(160)
                        .BorderBottom(1).BorderColor(Line);
                });
            });
        });
    }
}
