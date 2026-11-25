using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Jama.Application.Boqs.Queries.GetBoqPdf;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using Jama.Infrastructure.Data;
using Jama.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp.PixelFormats;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Jama.Application.Tests;

/// <summary>
/// The quotation downloads as PDF, Excel or Word. Word is the whole quotation (cover, letter with terms, items with
/// photos and descriptions, totals); Excel is the priced items only.
/// </summary>
public class BoqExportTests
{
    private static readonly BoqPdfGenerator Pdf = new();

    private static byte[] Photo()
    {
        using var image = new SixLabors.ImageSharp.Image<Rgba32>(400, 300, new Rgba32(37, 148, 210));
        using var stream = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(image, stream);
        return stream.ToArray();
    }

    private static BoqPdfModel Model(BoqTermsLanguage language = BoqTermsLanguage.English, bool terms = true, bool draft = false) =>
        new("JMG-00004", "Pearl Tower — Lobby CCTV", "Doha", "UDC", "30106239", new DateOnly(2026, 11, 20), "Approved",
            "Installation within 2 weeks of the P.O.", "Murshid <murshid@jamago.qa>", 7439m, 439m, 7000m,
            [
                new BoqPdfSection("1", "Cameras", 6530m,
                [
                    new BoqPdfLine("1.1", "2MP Bullet Camera", "DH-IPC-HFW2241T-ZS", "Dahua", "Bullet", "Piece", 10, 360m, 3600m)
                    {
                        Description = "Smart IR up to 60 m, IP67", DescriptionAr = "رؤية ليلية حتى 60 متر", Image = Photo(),
                    },
                    new BoqPdfLine("1.2", "2MP Dome Camera", "DH-IPC-HDBW2241E-S", "Dahua", "Dome", "Piece", 10, 293m, 2930m),
                ]),
                new BoqPdfSection("2", "Recorder", 909m,
                [
                    new BoqPdfLine("2.1", "16 Channel NVR", "DHI-NVR5816-EI", "Dahua", "NVR", "Piece", 1, 909m, 909m),
                ]),
            ])
        {
            RevisionNumber = 2,
            IncludeTermsPage = terms,
            TermsLanguage = language,
            Watermark = draft,
            PreparedByDesignation = "Head of Sales",
        };

    private static string WordText(byte[] docx, out WordprocessingDocument doc)
    {
        doc = WordprocessingDocument.Open(new MemoryStream(docx), false);
        return string.Join(" ", doc.MainDocumentPart!.Document.Body!.Descendants<W.Paragraph>().Select(p => p.InnerText));
    }

    private static void AssertValidWord(WordprocessingDocument doc)
    {
        var problems = new OpenXmlValidator().Validate(doc).Select(e => $"{e.Path?.XPath}: {e.Description}").ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems.Take(5)));
    }

    [Fact]
    public void Word_is_the_whole_quotation_and_opens_cleanly_in_Word()
    {
        var text = WordText(new BoqWordGenerator(Pdf).Generate(Model()), out var doc);
        using (doc)
        {
            AssertValidWord(doc);

            // Cover
            Assert.Contains("COMMERCIAL PROPOSAL", text);
            Assert.Contains("JMG-00004  ·  Rev 2", text);
            Assert.Contains("Prepared For:", text);
            Assert.Contains("UDC, Doha", text);
            Assert.Contains("PEARL TOWER — LOBBY CCTV", text);
            Assert.Contains("20th November 2026", text);
            Assert.Contains("info@jamago.qa", text);
            // Letter, with the standard terms and the signature
            Assert.Contains("Dear Sir / Madam,", text);
            Assert.Contains("1. Offer Validity and Price Escalation", text);
            Assert.Contains("Murshid", text);
            Assert.Contains("murshid@jamago.qa", text);
            Assert.Contains("Head of Sales", text);
            // Items, with descriptions in both languages, totals and words
            Assert.Contains("QUOTATION", text);
            Assert.Contains("1.  Cameras", text);
            Assert.Contains("2MP Bullet Camera", text);
            Assert.Contains("Smart IR up to 60 m, IP67", text);
            Assert.Contains("رؤية ليلية حتى 60 متر", text);
            Assert.Contains("Section total", text);
            Assert.Contains("6,530.00", text);
            Assert.Contains("FINAL AMOUNT (QAR)", text);
            Assert.Contains("7,000.00", text);
            Assert.Contains("Seven thousand riyals only", text);
            Assert.Contains("Installation within 2 weeks of the P.O.", text);
            Assert.DoesNotContain("DRAFT", text);

            // Logo (cover corner, cover letterhead, letter, items), QR (cover, letter) and the one product photo
            Assert.Equal(7, doc.MainDocumentPart!.ImageParts.Count());
            Assert.Single(doc.MainDocumentPart.FooterParts);
        }
    }

    [Theory]
    [InlineData(BoqTermsLanguage.English, true, false)]
    [InlineData(BoqTermsLanguage.Arabic, false, true)]
    [InlineData(BoqTermsLanguage.Both, true, true)]
    public void Word_letter_follows_the_chosen_terms_language(BoqTermsLanguage language, bool english, bool arabic)
    {
        var text = WordText(new BoqWordGenerator(Pdf).Generate(Model(language)), out var doc);
        using (doc)
        {
            AssertValidWord(doc);
            Assert.Equal(english, text.Contains("Offer Validity and Price Escalation"));
            var arabicHeading = Pdf.DefaultTermsTextArabic().Split('\n')[0];
            Assert.Equal(arabic, text.Contains(arabicHeading));
        }
    }

    [Fact]
    public void Word_without_the_terms_page_has_no_letter_and_a_draft_says_so()
    {
        var text = WordText(new BoqWordGenerator(Pdf).Generate(Model(terms: false, draft: true)), out var doc);
        using (doc)
        {
            AssertValidWord(doc);
            Assert.DoesNotContain("Dear Sir / Madam,", text);
            Assert.Contains("COMMERCIAL PROPOSAL", text);
            Assert.Contains("2MP Bullet Camera", text);
            Assert.Contains("DRAFT — NOT APPROVED", text);
        }
    }

    [Fact]
    public void Excel_is_the_priced_items_only()
    {
        using var book = new XLWorkbook(new MemoryStream(new BoqExcelGenerator().Generate(Model())));
        var ws = book.Worksheet(1);
        var cells = ws.CellsUsed().Select(c => c.GetFormattedString()).ToList();
        var all = string.Join(" | ", cells);

        Assert.Contains("QUOTATION", cells);
        Assert.Contains("JMG-00004  ·  Rev 2", all);
        Assert.Contains("UDC", cells);
        Assert.Contains("1.  Cameras", cells);
        Assert.Contains("2MP Bullet Camera", cells);
        Assert.Contains("Smart IR up to 60 m, IP67\nرؤية ليلية حتى 60 متر", cells);
        Assert.Contains("DH-IPC-HFW2241T-ZS", cells);
        Assert.Contains("Pcs", cells);
        Assert.Contains("Section total", cells);
        Assert.Contains("Seven thousand riyals only", all);
        Assert.Contains("Installation within 2 weeks of the P.O.", cells);
        Assert.Equal(7000m, ws.CellsUsed().Single(c => c.GetString().StartsWith("FINAL AMOUNT (QAR)")).WorksheetRow().Cell(10).GetValue<decimal>());
        Assert.Equal(-439m, ws.CellsUsed().Single(c => c.GetString().StartsWith("Discount")).WorksheetRow().Cell(10).GetValue<decimal>());

        // Items only: no cover, no letter, no pictures
        Assert.DoesNotContain(cells, c => c.Contains("COMMERCIAL PROPOSAL") || c.Contains("Dear Sir"));
        Assert.Empty(ws.Pictures);
        Assert.Equal(XLPageOrientation.Landscape, ws.PageSetup.PageOrientation);
    }

    [Theory]
    [InlineData(ExportFormat.Pdf, "JMG-00009.pdf", "application/pdf")]
    [InlineData(ExportFormat.Excel, "JMG-00009.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData(ExportFormat.Word, "JMG-00009.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public async Task The_quotation_downloads_in_the_chosen_format(ExportFormat format, string fileName, string contentType)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"boq-export-{Guid.NewGuid()}").Options);
        var boq = new Boq
        {
            Id = Guid.NewGuid(), BoqNumber = "JMG-00009", ProjectName = "Villa 9", ClientName = "Al Reem", IssueDate = new DateOnly(2026, 11, 20),
            Status = BoqStatus.Approved, PreparedById = Guid.NewGuid(), PreparedByName = "Khemaies", Total = 100m, GrandTotal = 100m,
            Sections = [new BoqSection { Id = Guid.NewGuid(), Title = "Cameras", Lines = [new BoqLine { Id = Guid.NewGuid(), ItemName = "4MP Dome", Uom = UnitOfMeasurement.Piece, Quantity = 1, UnitRate = 100m, LineTotal = 100m }] }],
        };
        db.Boqs.Add(boq);
        await db.SaveChangesAsync();

        var result = await new GetBoqPdfQueryHandler(db, Pdf, new NoFiles(), new Approver(), new BoqExcelGenerator(), new BoqWordGenerator(Pdf))
            .Handle(new GetBoqPdfQuery(boq.Id, Format: format), default);

        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        Assert.Equal(fileName, result.Data!.FileName);
        Assert.Equal(contentType, result.Data.ContentType);
        Assert.NotEmpty(result.Data.Content);
    }

    [Theory]
    [InlineData(null, ExportFormat.Pdf)]
    [InlineData("excel", ExportFormat.Excel)]
    [InlineData("WORD", ExportFormat.Word)]
    [InlineData("csv", null)]
    public void Format_names_are_read_as_expected(string? value, ExportFormat? expected) =>
        Assert.Equal(expected, ExportFormats.Parse(value));

    private sealed class Approver : ICurrentUser
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public string DisplayName => "Approver";
        public string Role => Roles.Staff;
        public bool Has(string permission) => permission == Permissions.BoqApprove;
        public bool IsSuperAdmin => false;
    }

    private sealed class NoFiles : IFileStorage
    {
        public Task<string> SaveAsync(Stream content, string storageKey, CancellationToken cancellationToken) => Task.FromResult(storageKey);
        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken) => Task.FromResult<Stream?>(null);
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
