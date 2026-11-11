using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.NonMoiJobs;
using Jama.Application.NonMoiJobs.Commands;
using Jama.Application.NonMoiJobs.Queries;
using Jama.Application.Options;
using Jama.Domain.Entities;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jama.Application.Tests;

/// <summary>Closing a Non-MOI deal: who may, what is checked, and that the client is never sent the same thing twice.</summary>
public class NonMoiJobClosingTests
{
    private static readonly Guid Issuer = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();

    private sealed class FakeUser(Guid id, string role = Roles.Staff) : ICurrentUser
    {
        public Guid UserId { get; } = id;
        public string DisplayName => "Sara";
        public string Role { get; } = role;
        public bool Has(string permission) => true;
        public bool IsSuperAdmin => false;
    }

    private sealed class FakeMail(bool ok = true) : IMailSender
    {
        public int Sent { get; private set; }
        public MailSendRequest? Last { get; private set; }
        public List<MailSendRequest> All { get; } = [];
        public Task<MailSendResult> SendAsync(MailSendRequest request, CancellationToken cancellationToken)
        {
            Last = request;
            All.Add(request);
            if (ok) Sent++;
            return Task.FromResult(ok ? new MailSendResult(true, null, 1, 10) : new MailSendResult(false, "Could not send: bad login"));
        }
    }

    private sealed class FakeWhatsApp(bool ok = true) : IWhatsAppSender
    {
        public int Sent { get; private set; }
        public string? LastUrl { get; private set; }
        public Task SendQuotationSentAsync(string toPhoneNumber, string customerName, string quoteNumber, decimal grandTotal, Guid quotationId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<WhatsAppSendResult> SendJobBillAsync(string toPhoneNumber, string customerName, string jobNumber, decimal amountPayable, string billUrl, CancellationToken cancellationToken)
        {
            LastUrl = billUrl;
            if (ok) Sent++;
            return Task.FromResult(new WhatsAppSendResult(ok, ok ? null : "WhatsApp could not deliver the message: number not on WhatsApp"));
        }
    }

    /// <summary>Holds the "background" work without running it, to prove the caller does not wait for it.</summary>
    private sealed class HeldWork : IBackgroundWork
    {
        public List<Func<CancellationToken, Task>> Items { get; } = [];
        public void Enqueue(Func<CancellationToken, Task> work) => Items.Add(work);
    }

    /// <summary>Runs the "background" work on the spot so a test can see what it did.</summary>
    private sealed class InlineWork : IBackgroundWork
    {
        public int Queued { get; private set; }
        public void Enqueue(Func<CancellationToken, Task> work) { Queued++; work(CancellationToken.None).GetAwaiter().GetResult(); }
    }

    private sealed class FakePdf : IJobBillPdfGenerator
    {
        public JobBillModel? Last { get; private set; }
        public byte[] Generate(JobBillModel model) { Last = model; return [37, 80, 68, 70]; }
    }

    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"close-{Guid.NewGuid()}").Options);

    private static IOptions<MailSettings> Mail() => Microsoft.Extensions.Options.Options.Create(new MailSettings
    {
        Accounts = [new MailAccountSettings { Key = "info", Address = "info@jamago.qa", Host = "imap.example.com", Password = "x", Signature = "Jama Go" }],
    });

    private static IOptions<TwilioSettings> Twilio(bool configured = true) => Microsoft.Extensions.Options.Options.Create(configured
        ? new TwilioSettings { AccountSid = "AC1", AuthToken = "t", WhatsAppFrom = "whatsapp:+14155238886", PublicApiBaseUrl = "https://api.example.com" }
        : new TwilioSettings());

    private static async Task<(ApplicationDbContext Context, NonMoiJob Job)> SeedAsync(string status = "Completed", Guid? issuer = null)
    {
        var context = NewContext();
        var job = new NonMoiJob
        {
            Id = Guid.NewGuid(), JobNumber = "NMJ-00001", CustomerName = "Al Noor", CustomerNumber = "+974 5550 1234", Location = "Doha",
            DescriptionEn = "Fix cable", AssignedToId = Guid.NewGuid(), AssignedToName = "Tech",
            AssignedById = issuer ?? Issuer, AssignedByName = "Sara", CustomerEmail = "client@example.com",
            Status = status, WorkTotal = 1000m, CompletedAt = DateTime.UtcNow,
            Lines = [new NonMoiJobLine { Id = Guid.NewGuid(), Category = "Camera", IssueType = "Camera replacement", Quantity = 2, UnitFee = 500m, LineTotal = 1000m }],
        };
        context.NonMoiJobs.Add(job);
        await context.SaveChangesAsync();
        return (context, job);
    }

    private static CloseNonMoiJobCommandHandler Handler(
        ApplicationDbContext context, ICurrentUser user, FakeMail? mail = null, FakeWhatsApp? whatsApp = null, FakePdf? pdf = null, bool twilio = true) =>
        new(context, user, mail ?? new FakeMail(), whatsApp ?? new FakeWhatsApp(), pdf ?? new FakePdf(), Mail(), Twilio(twilio), TimeProvider.System);

    private static CloseNonMoiJobCommand Close(Guid id, decimal discount = 0m, bool email = true, bool whatsApp = true, string? to = null) =>
        new(id, discount, to, email, whatsApp, "info");

    [Fact]
    public async Task The_issuer_closes_the_deal_with_a_discount_and_the_client_is_sent_the_bill_by_email()
    {
        var (context, job) = await SeedAsync();
        var mail = new FakeMail(); var wa = new FakeWhatsApp(); var pdf = new FakePdf();

        var result = await Handler(context, new FakeUser(Issuer), mail, wa, pdf).Handle(Close(job.Id, 100m), default);

        Assert.True(result.Succeeded);
        Assert.Equal("Sent", result.Data!.Status);        // now with the client, waiting for their answer
        Assert.Equal(100m, result.Data.Discount);
        Assert.Equal(900m, result.Data.AmountPayable);
        Assert.Equal(1, mail.Sent);
        Assert.Equal(0, wa.Sent);                          // WhatsApp is never used, even if it was asked for
        Assert.Equal(900m, pdf.Last!.AmountPayable);
        var attachment = Assert.Single(mail.Last!.Attachments!);
        Assert.Equal("NMJ-00001 Bill.pdf", attachment.FileName);
        Assert.Contains($"/bill/{job.BillToken}?do=approve", mail.Last.HtmlBody);
        Assert.Contains($"/bill/{job.BillToken}?do=reject", mail.Last.HtmlBody);
        Assert.Contains($"/bill/{job.BillToken}?do=comment", mail.Last.HtmlBody);
        Assert.Contains(">Approve<", mail.Last.HtmlBody);
        Assert.Contains("IBAN: <strong>QA11QIIB 0000 0000 1113196013001</strong>", mail.Last.HtmlBody);
        Assert.Contains("?do=approve", mail.Last.Body);          // the plain-text version has the same three links
        Assert.Contains("Jama Go", mail.Last.Body);      // the mailbox's signature is on the message
        Assert.Contains("FAWRAN - JAMA GO SECURITY EQUIPMENT", mail.Last.Body);   // where to pay
        Assert.Contains("CR NO: 189177", mail.Last.Body);
        Assert.Contains("IBAN: QA11QIIB 0000 0000 1113196013001", mail.Last.Body);
        Assert.Single(context.MailSendLogs);              // and the send is on record
    }

    [Fact]
    public async Task Any_non_moi_manager_may_send_the_bill_not_only_the_person_who_issued_the_job()
    {
        var (context, job) = await SeedAsync();                        // issued by Issuer

        var other = await Handler(context, new FakeUser(Stranger)).Handle(Close(job.Id), default);   // another Non-MOI staff member
        Assert.True(other.Succeeded);
        Assert.Equal("Sent", (await context.NonMoiJobs.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_job_issued_before_the_issuer_was_recorded_can_still_be_sent_by_a_manager()
    {
        var (context, job) = await SeedAsync(issuer: Guid.Empty);
        Assert.True((await Handler(context, new FakeUser(Issuer)).Handle(Close(job.Id), default)).Succeeded);
    }

    [Theory]
    [InlineData("Assigned")]
    [InlineData("Closed")]
    public async Task Only_a_completed_job_can_be_closed(string status)
    {
        var (context, job) = await SeedAsync(status);
        var mail = new FakeMail();

        var result = await Handler(context, new FakeUser(Issuer), mail).Handle(Close(job.Id), default);

        Assert.False(result.Succeeded);
        Assert.Equal(0, mail.Sent);
    }

    [Fact]
    public async Task The_discount_can_never_be_more_than_the_total_or_negative()
    {
        var (context, job) = await SeedAsync();
        Assert.False((await Handler(context, new FakeUser(Issuer)).Handle(Close(job.Id, 1000.01m), default)).Succeeded);
        Assert.True(new CloseNonMoiJobCommandValidator().Validate(Close(job.Id, -1m)).Errors.Count > 0);
        // The whole total is allowed — a free job is a decision the issuer may take.
        Assert.True((await Handler(context, new FakeUser(Issuer)).Handle(Close(job.Id, 1000m), default)).Succeeded);
    }

    [Fact]
    public void Email_is_mandatory_and_so_is_a_mailbox_to_send_it_from()
    {
        var validator = new CloseNonMoiJobCommandValidator();
        Assert.False(validator.Validate(Close(Guid.NewGuid(), email: false, whatsApp: false)).IsValid);
        Assert.False(validator.Validate(Close(Guid.NewGuid(), email: false, whatsApp: true)).IsValid);   // WhatsApp alone is not enough
        Assert.False(validator.Validate(new CloseNonMoiJobCommand(Guid.NewGuid(), 0m, null, true, false, null)).IsValid);
        Assert.False(validator.Validate(Close(Guid.NewGuid(), to: "not-an-email")).IsValid);
        Assert.True(validator.Validate(Close(Guid.NewGuid(), whatsApp: false)).IsValid);
    }

    [Fact]
    public async Task Without_an_email_on_file_the_email_must_be_typed()
    {
        var (context, job) = await SeedAsync();
        job.CustomerEmail = null;
        await context.SaveChangesAsync();

        Assert.False((await Handler(context, new FakeUser(Issuer)).Handle(Close(job.Id), default)).Succeeded);

        var typed = await Handler(context, new FakeUser(Issuer)).Handle(Close(job.Id, to: "new@example.com"), default);
        Assert.True(typed.Succeeded);
        Assert.Equal("new@example.com", typed.Data!.CustomerEmail);   // remembered on the job
    }

    [Fact]
    public async Task Whatsapp_is_never_used_even_when_it_is_requested_or_would_fail()
    {
        var (context, job) = await SeedAsync();
        var mail = new FakeMail(); var wa = new FakeWhatsApp(ok: false);

        var result = await Handler(context, new FakeUser(Issuer), mail, wa).Handle(Close(job.Id, 50m), default);   // whatsApp: true by default

        Assert.True(result.Succeeded);
        Assert.Equal(1, mail.Sent);
        Assert.Equal(0, wa.Sent);
        Assert.Null((await context.NonMoiJobs.SingleAsync()).WhatsAppSentAt);
        Assert.Equal("Sent", (await context.NonMoiJobs.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_failed_email_leaves_the_job_open_and_reports_why()
    {
        var (context, job) = await SeedAsync();
        var result = await Handler(context, new FakeUser(Issuer), new FakeMail(ok: false)).Handle(Close(job.Id), default);

        Assert.False(result.Succeeded);
        Assert.Contains("bad login", result.Errors.Single());
        Assert.Equal("Completed", (await context.NonMoiJobs.SingleAsync()).Status);
    }

    [Fact]
    public async Task The_bill_goes_by_email_whether_or_not_whatsapp_is_set_up_on_the_server()
    {
        var (context, job) = await SeedAsync();
        var mail = new FakeMail();
        var result = await Handler(context, new FakeUser(Issuer), mail, twilio: false).Handle(Close(job.Id), default);

        Assert.True(result.Succeeded);
        Assert.Equal(1, mail.Sent);
    }

    [Fact]
    public async Task A_request_with_email_turned_off_is_refused_and_nothing_is_sent()
    {
        var (context, job) = await SeedAsync();
        var mail = new FakeMail();
        var result = await Handler(context, new FakeUser(Issuer), mail).Handle(Close(job.Id, email: false), default);

        Assert.False(result.Succeeded);
        Assert.Equal(0, mail.Sent);
        Assert.Equal("Completed", (await context.NonMoiJobs.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_closed_job_cannot_be_edited_or_removed()
    {
        var (context, job) = await SeedAsync();
        await Handler(context, new FakeUser(Issuer)).Handle(Close(job.Id), default);

        var edit = await new UpdateNonMoiJobCommandHandler(context, TimeProvider.System).Handle(
            new UpdateNonMoiJobCommand { Id = job.Id, CustomerName = "Other", CustomerNumber = "55501234", Location = "x", DescriptionEn = "y", AssignedToId = job.AssignedToId }, default);
        var remove = await new DeleteNonMoiJobCommandHandler(context).Handle(new DeleteNonMoiJobCommand(job.Id), default);

        Assert.False(edit.Succeeded);
        Assert.False(remove.Succeeded);
    }

    [Fact]
    public async Task The_clients_link_works_only_for_a_closed_job_and_only_with_the_right_token()
    {
        var (context, job) = await SeedAsync();
        var pdf = new FakePdf();
        var byToken = new GetNonMoiJobBillByTokenQueryHandler(context, pdf);

        await Handler(context, new FakeUser(Issuer), pdf: pdf).Handle(Close(job.Id, 100m), default);
        var token = (await context.NonMoiJobs.SingleAsync()).BillToken!;

        Assert.True((await byToken.Handle(new GetNonMoiJobBillByTokenQuery(token), default)).Succeeded);
        Assert.False((await byToken.Handle(new GetNonMoiJobBillByTokenQuery(token[..^1] + (token[^1] == '0' ? "1" : "0")), default)).Succeeded);
        Assert.False((await byToken.Handle(new GetNonMoiJobBillByTokenQuery("abc"), default)).Succeeded);
        Assert.False((await byToken.Handle(new GetNonMoiJobBillByTokenQuery(""), default)).Succeeded);
    }

    [Fact]
    public void The_bill_prints_readable_category_names()
    {
        Assert.Equal("NVR & storage", Jama.Application.Maintenance.MaintenanceCategories.Label("NvrStorage"));
        Assert.Equal("Access Control", Jama.Application.Maintenance.MaintenanceCategories.Label("Access Control"));
    }

    [Fact]
    public async Task The_sidebar_count_is_every_finished_job_waiting_to_be_billed_for_all_managers()
    {
        var (context, job) = await SeedAsync();                       // done, issued by Issuer
        context.NonMoiJobs.Add(new NonMoiJob { Id = Guid.NewGuid(), JobNumber = "NMJ-2", CustomerName = "B", CustomerNumber = "5", Location = "x", DescriptionEn = "y", AssignedToId = Guid.NewGuid(), AssignedById = Stranger, Status = "Completed" });
        context.NonMoiJobs.Add(new NonMoiJob { Id = Guid.NewGuid(), JobNumber = "NMJ-3", CustomerName = "C", CustomerNumber = "5", Location = "x", DescriptionEn = "y", AssignedToId = Guid.NewGuid(), AssignedById = Issuer, Status = "Assigned" });   // not done yet
        await context.SaveChangesAsync();

        Assert.Equal(2, (await new GetJobsAwaitingCloseQueryHandler(context, new FakeUser(Issuer)).Handle(new GetJobsAwaitingCloseQuery(), default)).Data);
        Assert.Equal(2, (await new GetJobsAwaitingCloseQueryHandler(context, new FakeUser(Stranger)).Handle(new GetJobsAwaitingCloseQuery(), default)).Data);
        Assert.Equal(2, (await new GetJobsAwaitingCloseQueryHandler(context, new FakeUser(Guid.NewGuid(), Roles.Admin)).Handle(new GetJobsAwaitingCloseQuery(), default)).Data);

        await Handler(context, new FakeUser(Issuer)).Handle(Close(job.Id), default);     // sending one takes it off the count
        Assert.Equal(1, (await new GetJobsAwaitingCloseQueryHandler(context, new FakeUser(Issuer)).Handle(new GetJobsAwaitingCloseQuery(), default)).Data);
    }

    // ===== The client's answer =====

    private static RespondToNonMoiJobCommandHandler Responder(ApplicationDbContext context, FakeMail mail) =>
        new(context, mail, new InlineWork(), Mail(), TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RespondToNonMoiJobCommandHandler>.Instance);

    private static async Task<(ApplicationDbContext Context, NonMoiJob Job, FakeMail Mail)> SentAsync()
    {
        var (context, job) = await SeedAsync();
        context.AdminUsers.Add(new AdminUser { Id = Issuer, Email = "hasan@jamago.qa", FullName = "Hasan", IsActive = true });
        await context.SaveChangesAsync();
        var mail = new FakeMail();
        var sent = await Handler(context, new FakeUser(Issuer), mail).Handle(Close(job.Id, 100m, whatsApp: false), default);
        Assert.True(sent.Succeeded);
        mail.All.Clear();                       // from here on, only what the client's answer triggers
        return (context, job, mail);
    }

    [Fact]
    public async Task Approving_makes_the_deal_final_and_tells_the_company_the_issuer_and_the_client()
    {
        var (context, job, mail) = await SentAsync();

        var result = await Responder(context, mail).Handle(
            new RespondToNonMoiJobCommand(job.BillToken!, "approve", null, "Mr Noor"), default);

        Assert.True(result.Succeeded);
        var saved = await context.NonMoiJobs.SingleAsync();
        Assert.Equal("Closed", saved.Status);
        Assert.Equal("Approved", saved.ClientDecision);
        Assert.Equal("Mr Noor (client)", saved.ClosedByName);
        Assert.False(result.Data!.CanRespond);

        // Three notes: company mailbox + Hasan together, and the client's own receipt (which carries the payment details).
        Assert.Equal(2, mail.All.Count);
        var staff = mail.All[0];
        Assert.Contains("info@jamago.qa", staff.To);
        Assert.Contains("hasan@jamago.qa", staff.To);
        Assert.Contains("approved", staff.Subject);
        var client = mail.All[1];
        Assert.Equal(["client@example.com"], client.To);
        Assert.Contains("IBAN: QA11QIIB 0000 0000 1113196013001", client.Body);
        Assert.Contains(await context.NonMoiJobEvents.ToListAsync(), e => e.Action == "Approved");
    }

    [Theory]
    [InlineData("reject", "Rejected")]
    [InlineData("comment", "Commented")]
    public async Task Rejecting_or_commenting_sends_the_job_back_to_the_issuer_with_the_clients_words(string decision, string expected)
    {
        var (context, job, mail) = await SentAsync();

        var result = await Responder(context, mail).Handle(
            new RespondToNonMoiJobCommand(job.BillToken!, decision, "The price is too high for the cable work.", null), default);

        Assert.True(result.Succeeded);
        var saved = await context.NonMoiJobs.SingleAsync();
        Assert.Equal("ChangesRequested", saved.Status);
        Assert.Equal(expected, saved.ClientDecision);
        Assert.Equal("The price is too high for the cable work.", saved.ClientMessage);
        Assert.Contains("The price is too high", mail.All[0].Body);   // the company and Hasan are told what the client said
        Assert.Null(saved.ClosedAt);

        // …and it is waiting for the issuer: it shows in their red number.
        var waiting = await new GetJobsAwaitingCloseQueryHandler(context, new FakeUser(Issuer)).Handle(new GetJobsAwaitingCloseQuery(), default);
        Assert.Equal(1, waiting.Data);
    }

    [Fact]
    public async Task A_reject_or_comment_needs_a_few_words_but_an_approval_does_not()
    {
        var v = new RespondToNonMoiJobCommandValidator();
        Assert.False(v.Validate(new RespondToNonMoiJobCommand(new string('a', 48), "reject", null, null)).IsValid);
        Assert.False(v.Validate(new RespondToNonMoiJobCommand(new string('a', 48), "comment", "x", null)).IsValid);
        Assert.True(v.Validate(new RespondToNonMoiJobCommand(new string('a', 48), "comment", "Please recheck the cable length.", null)).IsValid);
        Assert.True(v.Validate(new RespondToNonMoiJobCommand(new string('a', 48), "approve", null, null)).IsValid);
        Assert.False(v.Validate(new RespondToNonMoiJobCommand(new string('a', 48), "maybe", "hmm hmm", null)).IsValid);
        Assert.False(v.Validate(new RespondToNonMoiJobCommand(new string('a', 48), "approve", new string('x', 1001), null)).IsValid);
    }

    [Fact]
    public async Task After_a_change_request_the_issuer_resends_a_new_revision_and_the_whole_story_is_kept()
    {
        var (context, job, mail) = await SentAsync();
        await Responder(context, mail).Handle(new RespondToNonMoiJobCommand(job.BillToken!, "comment", "Can you give a better price?", null), default);
        mail.All.Clear();

        var resend = await Handler(context, new FakeUser(Issuer), mail).Handle(Close(job.Id, 250m, whatsApp: false), default);

        Assert.True(resend.Succeeded);
        Assert.Equal("Sent", resend.Data!.Status);
        Assert.Equal(2, resend.Data.BillRevision);
        Assert.Equal(750m, resend.Data.AmountPayable);
        var email = Assert.Single(mail.All);                       // a fresh email went out, because this is a new revision
        Assert.Contains("revision 2", email.Subject);
        Assert.Equal(new[] { "Sent", "Commented", "Sent" }, resend.Data.Events!.Select(e => e.Action).ToArray());
        Assert.Equal(new[] { 1, 1, 2 }, resend.Data.Events!.Select(e => e.Revision).ToArray());

        // The same link now answers the new revision.
        var again = await Responder(context, mail).Handle(new RespondToNonMoiJobCommand(job.BillToken!, "approve", null, null), default);
        Assert.True(again.Succeeded);
        Assert.Equal("Closed", (await context.NonMoiJobs.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_bill_can_only_be_answered_while_it_is_waiting_and_an_unknown_link_does_nothing()
    {
        var (context, job, mail) = await SentAsync();
        await Responder(context, mail).Handle(new RespondToNonMoiJobCommand(job.BillToken!, "approve", null, null), default);

        var second = await Responder(context, mail).Handle(new RespondToNonMoiJobCommand(job.BillToken!, "reject", "changed my mind", null), default);
        Assert.False(second.Succeeded);
        Assert.Equal("Closed", (await context.NonMoiJobs.SingleAsync()).Status);

        var wrong = await Responder(context, mail).Handle(new RespondToNonMoiJobCommand(new string('0', 48), "approve", null, null), default);
        Assert.False(wrong.Succeeded);
    }

    [Fact]
    public async Task A_notification_that_fails_to_send_never_undoes_the_clients_answer()
    {
        var (context, job, _) = await SentAsync();

        var result = await Responder(context, new FakeMail(ok: false)).Handle(new RespondToNonMoiJobCommand(job.BillToken!, "approve", null, null), default);

        Assert.True(result.Succeeded);
        Assert.Equal("Closed", (await context.NonMoiJobs.SingleAsync()).Status);
    }

    [Fact]
    public async Task The_clients_page_shows_the_bill_without_internal_details_and_only_invites_an_answer_while_waiting()
    {
        var (context, job, mail) = await SentAsync();
        var view = new GetPublicJobBillQueryHandler(context);

        var waiting = (await view.Handle(new GetPublicJobBillQuery(job.BillToken!), default)).Data!;
        Assert.True(waiting.CanRespond);
        Assert.Equal(900m, waiting.AmountPayable);
        Assert.Equal("FAWRAN - JAMA GO SECURITY EQUIPMENT", waiting.PaymentAccountName);
        Assert.Equal("Camera", waiting.Lines.Single().Category);

        await Responder(context, mail).Handle(new RespondToNonMoiJobCommand(job.BillToken!, "approve", null, null), default);
        Assert.False((await view.Handle(new GetPublicJobBillQuery(job.BillToken!), default)).Data!.CanRespond);

        Assert.False((await view.Handle(new GetPublicJobBillQuery("short"), default)).Succeeded);
    }

    [Fact]
    public async Task The_clients_answer_returns_before_any_notification_email_is_sent()
    {
        var (context, job, mail) = await SentAsync();
        var held = new HeldWork();
        var handler = new RespondToNonMoiJobCommandHandler(context, mail, held, Mail(), TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RespondToNonMoiJobCommandHandler>.Instance);

        var result = await handler.Handle(new RespondToNonMoiJobCommand(job.BillToken!, "approve", null, null), default);

        Assert.True(result.Succeeded);
        Assert.Equal("Closed", (await context.NonMoiJobs.SingleAsync()).Status);   // saved…
        Assert.Empty(mail.All);                                                    // …and nothing has been emailed yet
        Assert.Single(held.Items);                                                 // the emails are waiting for the background worker

        await held.Items[0](CancellationToken.None);                               // when the worker gets to it, they go out
        Assert.Equal(2, mail.All.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_page_is_told_whether_whatsapp_is_available_and_which_mailboxes_can_send(bool configured)
    {
        var result = await new GetBillMailboxesQueryHandler(Mail(), Twilio(configured)).Handle(new GetBillMailboxesQuery(), default);

        Assert.Equal(configured, result.Data!.WhatsAppAvailable);
        var box = Assert.Single(result.Data.Mailboxes);
        Assert.Equal("info", box.Key);
        Assert.Equal("info@jamago.qa", box.Address);
    }
}
