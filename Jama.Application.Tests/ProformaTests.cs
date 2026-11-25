using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Proformas;
using Jama.Application.Proformas.Commands.SaveProforma;
using Jama.Application.Proformas.Queries.GetAdminQuotations;
using Jama.Application.Proformas.Queries.GetProformaFile;
using Jama.Application.Proformas.Queries.GetProformaSheet;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using Jama.Infrastructure.Data;
using Jama.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace Jama.Application.Tests;

/// <summary>
/// The administrator's section: every quotation in any status, and the short proforma invoice of each — a kept
/// PI number and date for an approved quotation, the client's P.O., and no descriptions or pictures.
/// </summary>
public class ProformaTests
{
    private sealed class Admin : ICurrentUser
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public string DisplayName => "Office Admin <office@jamago.qa>";
        public string Role => "Staff";
        public bool Has(string permission) => permission == Permissions.ProformaManage;
        public bool IsSuperAdmin => false;
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private const string CameraDescription = "Smart IR up to 60 m, IP67 weatherproof, built-in mic";

    private static readonly Guid Murshid = Guid.NewGuid();
    private static readonly Guid Khemaies = Guid.NewGuid();

    // 23 Nov 2026, 22:30 UTC = 24 Nov in Doha.
    private static Clock LateEvening() => new(new DateTimeOffset(2026, 11, 23, 22, 30, 0, TimeSpan.Zero));

    private static async Task<(ApplicationDbContext Db, Boq Approved, Boq Draft, Boq Deleted)> SeedAsync()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"proforma-{Guid.NewGuid()}").Options);
        db.AdminUsers.AddRange(
            new AdminUser { Id = Murshid, FullName = "Murshid", Email = "murshid@jamago.qa", Role = "Staff" },
            new AdminUser { Id = Khemaies, FullName = "Khemaies", Email = "khemaies@jamago.qa", Role = "Staff" });
        var camera = new Camera { Id = Guid.NewGuid(), ItemName = "2MP Bullet", DescriptionEn = CameraDescription };
        db.Cameras.Add(camera);

        BoqLine Line(string name, string brand, string model, UnitOfMeasurement uom, decimal qty, decimal rate, int order, Guid? cameraId = null) =>
            new() { Id = Guid.NewGuid(), ItemName = name, Brand = brand, ModelNo = model, Uom = uom, Quantity = qty, UnitRate = rate, LineTotal = qty * rate, SortOrder = order, CameraId = cameraId };

        var approved = new Boq
        {
            Id = Guid.NewGuid(), BoqNumber = "JMG-00052", ProjectName = "BUZWAIR HOLDING BUILDING 24", ClientName = "BUZWAIR REAL ESTATE WLL",
            ContactNumber = "40150800", SiteLocation = "Old Airport, Doha - Qatar", IssueDate = new DateOnly(2026, 11, 8),
            Status = BoqStatus.Approved, PreparedById = Murshid, PreparedByName = "Old Name <murshid@jamago.qa>",
            Total = 7439m, SpecialDiscount = 439m, GrandTotal = 7000m, CreatedAt = new DateTime(2026, 11, 8, 6, 0, 0, DateTimeKind.Utc),
            Sections =
            [
                new BoqSection { Id = Guid.NewGuid(), Title = "Cameras", SortOrder = 0, Lines =
                [
                    Line("2MP IR Vari-focal Bullet WizMind Network Camera", "Dahua", "DH-IPC-HFW2241T-ZS", UnitOfMeasurement.Piece, 10, 360m, 0, camera.Id),
                    Line("2MP IR Fixed-focal Dome WizSense Network Camera", "Dahua", "DH-IPC-HDBW2241E-S", UnitOfMeasurement.Piece, 10, 293m, 1),
                ]},
                new BoqSection { Id = Guid.NewGuid(), Title = "Service & MOI approval", SortOrder = 1, Lines =
                [
                    Line("Cable Pulling, Installation & Termination of Network Cables", "JAMA GO", "Services", UnitOfMeasurement.Lot, 1, 909m, 0),
                ]},
            ],
        };
        var draft = new Boq
        {
            Id = Guid.NewGuid(), BoqNumber = "JMG-00060", ProjectName = "Villa 12", ClientName = "Al Reem", IssueDate = new DateOnly(2026, 11, 20),
            Status = BoqStatus.Draft, PreparedById = Khemaies, PreparedByName = "Khemaies <khemaies@jamago.qa>", Total = 100m, GrandTotal = 100m,
            CreatedAt = new DateTime(2026, 11, 20, 6, 0, 0, DateTimeKind.Utc),
            Sections = [new BoqSection { Id = Guid.NewGuid(), Title = "Cameras", Lines = [Line("4MP Dome", "Hikvision", "DS-2CD", UnitOfMeasurement.Piece, 1, 100m, 0)] }],
        };
        var deleted = new Boq
        {
            Id = Guid.NewGuid(), BoqNumber = "JMG-00061", ProjectName = "Gone", Status = BoqStatus.Approved, IsDeleted = true,
            PreparedById = Khemaies, PreparedByName = "Khemaies", CreatedAt = new DateTime(2026, 11, 21, 6, 0, 0, DateTimeKind.Utc),
        };
        db.Boqs.AddRange(approved, draft, deleted);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (db, approved, draft, deleted);
    }

    private static GetProformaFileQueryHandler Files(ApplicationDbContext db, TimeProvider clock) =>
        new(db, new ProformaPdfGenerator(), new ProformaExcelGenerator(), new ProformaWordGenerator(), clock);

    private static string Text(byte[] pdf)
    {
        using var doc = PdfDocument.Open(pdf);
        return string.Join(" ", doc.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));
    }

    [Fact]
    public async Task An_approved_quotation_gets_a_kept_PI_number_and_date_and_the_PO_can_be_changed()
    {
        var (db, approved, _, _) = await SeedAsync();
        var clock = LateEvening();
        var save = new SaveProformaCommandHandler(db, new Admin(), clock);

        var first = await save.Handle(new SaveProformaCommand(approved.Id, " BRE-PO-26-169 ", new DateOnly(2026, 11, 10)), default);
        Assert.True(first.Succeeded, string.Join(", ", first.Errors));
        Assert.Equal("PI-JG-00001", first.Data!.Number);
        Assert.Equal(new DateOnly(2026, 11, 24), first.Data.IssuedOn);   // Doha day, not UTC day
        Assert.Equal("BRE-PO-26-169", first.Data.PoNumber);

        clock.Now = clock.Now.AddDays(5);
        var again = await save.Handle(new SaveProformaCommand(approved.Id, "BRE-PO-26-170", null), default);
        Assert.Equal("PI-JG-00001", again.Data!.Number);                  // same number
        Assert.Equal(new DateOnly(2026, 11, 24), again.Data.IssuedOn);   // same date
        Assert.Equal("BRE-PO-26-170", again.Data.PoNumber);
        Assert.Null(again.Data.PoDate);
        Assert.Equal(1, await db.ProformaInvoices.CountAsync());
    }

    [Fact]
    public async Task Customer_details_come_from_the_quotation_and_can_be_changed_for_the_proforma_only()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var (db, approved, _, _) = await SeedAsync();
        var clock = LateEvening();
        var save = new SaveProformaCommandHandler(db, new Admin(), clock);

        // Name changed, project typed the same as the quotation, contact left empty, location changed.
        var saved = await save.Handle(new SaveProformaCommand(approved.Id, null, null,
            new ProformaCustomer(" Buzwair Holding W.L.L. ", "BUZWAIR HOLDING BUILDING 24", "  ", "Salwa Road, Doha")), default);
        Assert.True(saved.Succeeded, string.Join(", ", saved.Errors));
        Assert.Equal("Buzwair Holding W.L.L.", saved.Data!.CustomerName);
        Assert.Null(saved.Data.ProjectName);          // same as the quotation: keeps following it
        Assert.Null(saved.Data.ContactNumber);        // empty: back to the quotation's
        Assert.Equal("Salwa Road, Doha", saved.Data.Location);

        var quotation = await db.Boqs.AsNoTracking().SingleAsync(b => b.Id == approved.Id);
        Assert.Equal("BUZWAIR REAL ESTATE WLL", quotation.ClientName);   // the quotation itself is untouched
        Assert.Equal("Old Airport, Doha - Qatar", quotation.SiteLocation);

        var pdf = await Files(db, clock).Handle(new GetProformaFileQuery(approved.Id), default);
        var text = Text(pdf.Data!.Content);
        Assert.Contains("Buzwair Holding W", text);
        Assert.DoesNotContain("BUZWAIR REAL ESTATE WLL", text);
        Assert.Contains("BUZWAIR HOLDING BUILDING 24", text);
        Assert.Contains("40150800", text);
        Assert.Contains("Salwa Road, Doha", text);

        // Saving the P.O. alone (no customer details sent) keeps the changed details.
        var po = await save.Handle(new SaveProformaCommand(approved.Id, "BRE-PO-26-169", null), default);
        Assert.Equal("Buzwair Holding W.L.L.", po.Data!.CustomerName);
    }

    [Fact]
    public async Task The_preview_prints_customer_details_as_typed()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var (db, approved, _, _) = await SeedAsync();

        var result = await Files(db, LateEvening()).Handle(
            new GetProformaFileQuery(approved.Id, ProformaFormat.Pdf, new ProformaInput(null, null, new ProformaCustomer("Typed Client", null, "5555 1234", null))), default);

        var text = Text(result.Data!.Content);
        Assert.Contains("Typed Client", text);
        Assert.Contains("5555 1234", text);
        Assert.Contains("BUZWAIR HOLDING BUILDING 24", text);    // empty: the quotation's
        Assert.Empty(db.ProformaInvoices);
    }

    [Fact]
    public async Task The_on_screen_invoice_shows_what_the_PDF_prints()
    {
        var (db, approved, draft, _) = await SeedAsync();
        var clock = LateEvening();
        await new SaveProformaCommandHandler(db, new Admin(), clock).Handle(new SaveProformaCommand(approved.Id, "BRE-PO-26-169", null,
            new ProformaCustomer("Buzwair Holding", null, null, null)), default);
        var sheet = new GetProformaSheetQueryHandler(db, clock);

        var s = (await sheet.Handle(new GetProformaSheetQuery(approved.Id), default)).Data!;
        Assert.Equal("PI-JG-00001", s.Document.Number);
        Assert.Equal("Buzwair Holding", s.Document.ClientName);            // as changed for the proforma
        Assert.Equal("BUZWAIR REAL ESTATE WLL", s.Quotation.Name);         // the quotation's own, to go back to
        Assert.Equal("BRE-PO-26-169", s.Document.PoNumber);
        Assert.Equal("Murshid", s.Document.Ref);
        Assert.Equal([1, 2, 3], s.Document.Sections.SelectMany(x => x.Lines).Select(l => l.Number));
        Assert.Equal("Pcs", s.Document.Sections[0].Lines[0].Unit);
        Assert.Equal("Seven thousand riyals only", s.AmountInWords);
        Assert.Equal(2, s.Terms.Count);
        Assert.Equal("QIIBQAQA", s.Payment.SwiftCode);
        Assert.Equal("JAMA GO SECURITY EQUIPMENT", s.Company.Name);

        var d = (await sheet.Handle(new GetProformaSheetQuery(draft.Id), default)).Data!;
        Assert.True(d.Document.Draft);
        Assert.Null(d.Document.Number);
        Assert.Null(d.Proforma);
    }

    [Fact]
    public async Task Excel_and_Word_hold_the_same_proforma_as_the_PDF()
    {
        var (db, approved, draft, _) = await SeedAsync();
        var clock = LateEvening();
        await new SaveProformaCommandHandler(db, new Admin(), clock).Handle(new SaveProformaCommand(approved.Id, "BRE-PO-26-169", new DateOnly(2026, 11, 10)), default);
        var files = Files(db, clock);

        // Excel
        var xlsx = (await files.Handle(new GetProformaFileQuery(approved.Id, ProformaFormat.Excel), default)).Data!;
        Assert.Equal("PI-JG-00001.xlsx", xlsx.FileName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.ContentType);
        using (var book = new ClosedXML.Excel.XLWorkbook(new MemoryStream(xlsx.Content)))
        {
            var ws = book.Worksheet(1);
            var cells = ws.CellsUsed().Select(c => c.GetFormattedString()).ToList();
            var all = string.Join(" | ", cells);
            Assert.Contains("PROFORMA INVOICE", cells);
            Assert.Contains("PI-JG-00001", cells);
            Assert.Contains("SNO", cells);
            Assert.Contains("CAMERAS", cells);
            Assert.Contains("2MP IR Vari-focal Bullet WizMind Network Camera", cells);
            Assert.Contains("Pcs", cells);
            Assert.Contains("Name : BUZWAIR REAL ESTATE WLL", all);
            Assert.Contains("Notes: P.O. No. BRE-PO-26-169 dated 10-11-2026", all);
            Assert.Contains("Seven thousand riyals only", all);
            Assert.Contains("QIIBQAQA", cells);
            Assert.Contains(ProformaText.CompanyAddress, cells);
            Assert.Equal(7000m, ws.CellsUsed().Single(c => c.GetString() == "Net Total (QAR)").WorksheetRow().Cell(8).GetValue<decimal>());
            Assert.Empty(ws.Pictures);                                    // no logo in Excel
            Assert.DoesNotContain(cells, c => c.Contains("IP67"));        // no catalogue description
        }

        // Word
        var docx = (await files.Handle(new GetProformaFileQuery(approved.Id, ProformaFormat.Word), default)).Data!;
        Assert.Equal("PI-JG-00001.docx", docx.FileName);
        using (var word = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(new MemoryStream(docx.Content), false))
        {
            var text = string.Join(" ", word.MainDocumentPart!.Document.Body!
                .Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>().Select(x => x.InnerText));
            Assert.Contains("PROFORMA INVOICE", text);
            Assert.Contains("PI-JG-00001", text);
            Assert.Contains("SNO", text);
            Assert.Contains("CAMERAS", text);
            Assert.Contains("DH-IPC-HFW2241T-ZS", text);
            Assert.Contains("Name : BUZWAIR REAL ESTATE WLL", text);
            Assert.Contains("P.O. No. BRE-PO-26-169 dated 10-11-2026", text);
            Assert.Contains("7,000.00", text);
            Assert.Contains("Seven thousand riyals only", text);
            Assert.Contains("QIIBQAQA", text);
            Assert.Contains("Mobile +974 3064 4006 | +974 3139 5879", text);
            Assert.DoesNotContain("IP67", text);
            Assert.Single(word.MainDocumentPart.ImageParts);              // the logo
            Assert.Single(word.MainDocumentPart.FooterParts);             // page numbers

            // Microsoft's own checker: anything it reports would make Word call the file damaged.
            var problems = new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(word)
                .Select(e => $"{e.Path?.XPath}: {e.Description}").ToList();
            Assert.True(problems.Count == 0, string.Join("\n", problems.Take(5)));
        }

        // A draft says so in every format.
        var draftXlsx = (await files.Handle(new GetProformaFileQuery(draft.Id, ProformaFormat.Excel), default)).Data!;
        Assert.StartsWith("Proforma-DRAFT-", draftXlsx.FileName);
        using var draftBook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(draftXlsx.Content));
        Assert.Contains(draftBook.Worksheet(1).CellsUsed(), c => c.GetString() == "DRAFT — NOT APPROVED");
    }

    [Fact]
    public async Task A_quotation_that_is_not_approved_gets_no_number()
    {
        var (db, _, draft, _) = await SeedAsync();
        var result = await new SaveProformaCommandHandler(db, new Admin(), LateEvening()).Handle(new SaveProformaCommand(draft.Id, null, null), default);

        Assert.False(result.Succeeded);
        Assert.Equal(ProformaRules.NotApproved, result.Errors.Single());
        Assert.Empty(db.ProformaInvoices);
    }

    [Fact]
    public void A_PO_date_needs_a_PO_number()
    {
        var validator = new SaveProformaCommandValidator();
        Assert.False(validator.Validate(new SaveProformaCommand(Guid.NewGuid(), " ", new DateOnly(2026, 11, 10))).IsValid);
        Assert.True(validator.Validate(new SaveProformaCommand(Guid.NewGuid(), "PO-1", null)).IsValid);
    }

    [Fact]
    public async Task The_list_shows_every_staff_members_quotations_in_any_status_but_not_deleted_ones()
    {
        var (db, approved, draft, _) = await SeedAsync();
        await new SaveProformaCommandHandler(db, new Admin(), LateEvening()).Handle(new SaveProformaCommand(approved.Id, "BRE-PO-26-169", null), default);
        var list = new GetAdminQuotationsQueryHandler(db);

        var all = (await list.Handle(new GetAdminQuotationsQuery(), default)).Data!;
        Assert.Equal(["JMG-00060", "JMG-00052"], all.Items.Select(x => x.BoqNumber));   // newest first, deleted one gone
        Assert.Equal("PI-JG-00001", all.Items.Single(x => x.Id == approved.Id).Proforma!.Number);
        Assert.Null(all.Items.Single(x => x.Id == draft.Id).Proforma);

        var drafts = (await list.Handle(new GetAdminQuotationsQuery { Status = BoqStatus.Draft }, default)).Data!;
        Assert.Equal("JMG-00060", drafts.Items.Single().BoqNumber);

        Assert.Equal("JMG-00052", (await list.Handle(new GetAdminQuotationsQuery { Search = "pi-jg-00001" }, default)).Data!.Items.Single().BoqNumber);
        Assert.Equal("JMG-00052", (await list.Handle(new GetAdminQuotationsQuery { Search = "bre-po" }, default)).Data!.Items.Single().BoqNumber);
        Assert.Equal("JMG-00060", (await list.Handle(new GetAdminQuotationsQuery { Search = "khemaies" }, default)).Data!.Items.Single().BoqNumber);
    }

    [Fact]
    public async Task The_proforma_PDF_is_the_short_one_with_PI_number_PO_and_totals_and_no_descriptions()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var (db, approved, _, _) = await SeedAsync();
        var clock = LateEvening();
        await new SaveProformaCommandHandler(db, new Admin(), clock).Handle(new SaveProformaCommand(approved.Id, "BRE-PO-26-169", new DateOnly(2026, 11, 10)), default);

        var result = await Files(db, clock).Handle(new GetProformaFileQuery(approved.Id), default);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        Assert.Equal("PI-JG-00001.pdf", result.Data!.FileName);

        var text = Text(result.Data.Content);
        Assert.Contains("PROFORMA INVOICE", text);
        Assert.Contains("PI-JG-00001", text);
        Assert.Contains("JMG-00052", text);
        Assert.Contains("24-11-2026", text);                      // the proforma's date
        Assert.Contains("08-11-2026", text);                      // the quote's date
        Assert.Contains("Murshid", text);                         // Ref#: today's name of who prepared it
        Assert.DoesNotContain("Old Name", text);
        Assert.Contains("BUZWAIR REAL ESTATE WLL", text);
        Assert.Contains("BRE-PO-26-169 dated 10-11-2026", text);   // the P.O. note (PdfPig drops bold dots, so no "P.O. No.")
        Assert.Contains("SNO DESCRIPTION BRAND", text);          // the first column is SNO
        Assert.Contains("CAMERAS", text);
        Assert.Contains("SERVICE & MOI APPROVAL", text);
        Assert.Contains("DH-IPC-HFW2241T-ZS", text);
        Assert.Contains("Pcs", text);
        Assert.Contains("7,439.00", text);                        // subtotal
        Assert.Contains("439.00", text);                          // discount
        Assert.Contains("7,000.00", text);                        // net total
        Assert.Contains("Seven thousand riyals only", text);
        Assert.Contains("Qatari Riyals", text);                   // the standing terms
        Assert.Contains("approved by both parties", text);
        Assert.Contains("1113196013001", text);                   // payment details
        Assert.Contains("QIIBQAQA", text);
        Assert.Contains("SALWA ROAD", text);
        Assert.Contains("JAMA GO SECURITY EQUIPMENT", text);      // footer
        Assert.Contains("3064 4006", text);
        Assert.DoesNotContain("IP67", text);                      // no catalogue description
        Assert.DoesNotContain("DRAFT", text);
    }

    [Fact]
    public async Task A_quotation_that_is_not_approved_downloads_as_a_numberless_draft_preview()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var (db, _, draft, _) = await SeedAsync();

        var result = await Files(db, LateEvening()).Handle(new GetProformaFileQuery(draft.Id), default);

        Assert.True(result.Succeeded);
        Assert.Equal("Proforma-DRAFT-JMG-00060.pdf", result.Data!.FileName);
        var text = Text(result.Data.Content);
        Assert.Contains("DRAFT", text);
        Assert.DoesNotContain("PI-JG-", text);
        Assert.Empty(db.ProformaInvoices);                        // looking does not use up a number
    }

    [Fact]
    public async Task The_preview_shows_the_typed_PO_and_saves_nothing()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var (db, approved, _, _) = await SeedAsync();

        var result = await Files(db, LateEvening())
            .Handle(new GetProformaFileQuery(approved.Id, ProformaFormat.Pdf, new ProformaInput(" BRE-PO-26-169 ", new DateOnly(2026, 11, 10), ProformaCustomer.AsQuotation)), default);

        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        var text = Text(result.Data!.Content);
        Assert.Contains("Given on download", text);              // no number used up
        Assert.Contains("BRE-PO-26-169 dated 10-11-2026", text);  // the P.O. as typed
        Assert.DoesNotContain("PI-JG-", text);
        Assert.DoesNotContain("DRAFT", text);
        Assert.Empty(db.ProformaInvoices);
    }

    [Fact]
    public void The_title_is_on_the_first_page_and_the_company_address_on_the_last_page_only()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var lines = Enumerable.Range(1, 120)
            .Select(i => new ProformaModelLine(i, $"Camera {i}", "Dahua", $"DH-{i}", "Piece", 1, 100m, 100m))
            .ToList();
        var model = new ProformaModel("PI-JG-00009", new DateOnly(2026, 11, 24), "JMG-00099", new DateOnly(2026, 11, 20), "Murshid",
            "Client", "Project", "40150800", "Doha", [new ProformaModelSection("Cameras", lines)], 12000m, 0m, 12000m, null, null, false);

        using var pdf = PdfDocument.Open(new ProformaPdfGenerator().Generate(model));
        Assert.True(pdf.NumberOfPages >= 2);
        string Page(int n) => string.Join(" ", pdf.GetPage(n).GetWords().Select(w => w.Text));

        Assert.Contains("PROFORMA INVOICE", Page(1));
        for (var n = 2; n <= pdf.NumberOfPages; n++)
            Assert.DoesNotContain("PROFORMA", Page(n));
        Assert.Contains("DESCRIPTION", Page(2));                 // the table headings still repeat on the next page
        for (var n = 1; n < pdf.NumberOfPages; n++)
        {
            Assert.DoesNotContain("Al Ain Complex", Page(n));
            Assert.DoesNotContain("3064 4006", Page(n));
        }
        var last = Page(pdf.NumberOfPages);
        Assert.Contains("Mobile", last);                          // the company block is never split across pages
        Assert.Contains("JAMA GO SECURITY EQUIPMENT", last);
        Assert.Contains("Building No 349, Al Ain Complex", last);
        Assert.Contains("Mobile +974 3064 4006 | +974 3139 5879", last);
    }

    [Fact]
    public void The_company_footer_never_sits_alone_on_an_empty_last_page()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var generator = new ProformaPdfGenerator();
        // Every length from a few lines to three pages: whatever the length, the last page also carries the end of
        // the document (the payment details), never just the company footer.
        for (var count = 1; count <= 110; count += 3)
        {
            var lines = Enumerable.Range(1, count)
                .Select(i => new ProformaModelLine(i, $"Camera {i}", "Dahua", $"DH-{i}", "Piece", 1, 100m, 100m))
                .ToList();
            var model = new ProformaModel("PI-JG-00009", new DateOnly(2026, 11, 24), "JMG-00099", new DateOnly(2026, 11, 20), "Murshid",
                "Client", "Project", "40150800", "Doha", [new ProformaModelSection("Cameras", lines)], count * 100m, 0m, count * 100m, "PO-1", null, false);

            using var pdf = PdfDocument.Open(generator.Generate(model));
            var last = string.Join(" ", pdf.GetPage(pdf.NumberOfPages).GetWords().Select(w => w.Text));
            Assert.True(last.Contains("QIIBQAQA"), $"{count} lines: the last page holds only the footer");
            Assert.Contains("Al Ain Complex", last);
        }
    }

    [Fact]
    public async Task An_approved_quotation_needs_its_proforma_made_before_download()
    {
        var (db, approved, _, _) = await SeedAsync();
        var result = await Files(db, LateEvening()).Handle(new GetProformaFileQuery(approved.Id), default);
        Assert.False(result.Succeeded);
    }
}
