using Jama.Application.MoiJobs.Services;
using FluentValidation;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Application.Maintenance;
using Jama.Application.Options;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jama.Application.MoiJobs;

public sealed record PublicBillLineDto(string Issue, string Category, int Quantity, decimal UnitFee, decimal LineTotal);

/// <summary>What the client's page shows. Nothing internal: no staff names, no ids, no other jobs.</summary>
public sealed record PublicJobBillDto(
    string JobNumber,
    string CustomerName,
    string Location,
    int Revision,
    string Status,
    IReadOnlyList<PublicBillLineDto> Lines,
    decimal Total,
    decimal Discount,
    decimal AmountPayable,
    string PaymentAccountName,
    string PaymentCrNumber,
    string PaymentIban,
    /// <summary>True while the bill is waiting for the client's answer.</summary>
    bool CanRespond,
    /// <summary>"Approved", "Rejected" or "Commented" once the client has answered this revision.</summary>
    string? Decision,
    string? DecisionMessage,
    DateTime? DecidedAt,
    /// <summary>The client's own number, shown under "Billed to".</summary>
    string CustomerNumber = "",
    DateTime? CompletedAt = null,
    /// <summary>When this revision of the bill was sent: the invoice date.</summary>
    DateTime? IssuedAt = null);

/// <summary>The client's page, found by the secret token in the link they were sent.</summary>
public sealed record GetPublicJobBillQuery(string Token) : IRequest<ApiResult<PublicJobBillDto>>;

public sealed class GetPublicJobBillQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPublicJobBillQuery, ApiResult<PublicJobBillDto>>
{
    public async Task<ApiResult<PublicJobBillDto>> Handle(GetPublicJobBillQuery request, CancellationToken cancellationToken)
    {
        var token = request.Token?.Trim();
        if (string.IsNullOrEmpty(token) || token.Length < 24)
            return ApiResult<PublicJobBillDto>.Failure("This link is not valid.");

        var job = await context.MoiJobs.AsNoTracking().Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.BillToken == token, cancellationToken);
        if (job is null || !MoiJobBill.IsSent(job))
            return ApiResult<PublicJobBillDto>.Failure("This link is not valid.");

        var issuedAt = await context.MoiJobEvents.AsNoTracking()
            .Where(e => e.MoiJobId == job.Id && e.Action == "Sent" && e.Revision == job.BillRevision)
            .Select(e => (DateTime?)e.CreatedAt).MaxAsync(cancellationToken);

        return ApiResult<PublicJobBillDto>.Success(new PublicJobBillDto(
            job.JobNumber, job.CustomerName, job.Location, job.BillRevision, job.Status,
            job.Lines.OrderBy(l => l.SortOrder)
                .Select(l => new PublicBillLineDto(l.IssueType, MoiServiceCategories.Label(l.Category), l.Quantity, l.UnitFee, l.LineTotal)).ToList(),
            job.WorkTotal, job.Discount, job.AmountPayable,
            CompanyPaymentDetails.AccountName, CompanyPaymentDetails.CrNumber, CompanyPaymentDetails.Iban,
            job.Status == "Sent", job.ClientDecision, job.ClientMessage, job.ClientRespondedAt,
            job.CustomerNumber, job.CompletedAt, issuedAt ?? job.EmailSentAt ?? job.WhatsAppSentAt ?? job.UpdatedAt));
    }
}

/// <summary>The client's answer: approve, reject (with a reason) or comment (asking for a change).</summary>
public sealed record RespondToMoiJobCommand(string Token, string? Decision, string? Message, string? Name)
    : IRequest<ApiResult<PublicJobBillDto>>;

public sealed class RespondToMoiJobCommandValidator : AbstractValidator<RespondToMoiJobCommand>
{
    public RespondToMoiJobCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Decision).Must(d => d is not null && new[] { "approve", "reject", "comment" }.Contains(d.Trim().ToLowerInvariant()))
            .WithMessage("Choose Approve, Reject or Comment.");
        RuleFor(x => x.Message).NotEmpty().WithMessage("Please write a few words, so we know what to change.")
            .MinimumLength(3).WithMessage("Please write a few words, so we know what to change.")
            .When(x => x.Decision is not null && x.Decision.Trim().ToLowerInvariant() is "reject" or "comment");
        RuleFor(x => x.Message).MaximumLength(1000).WithMessage("Please keep it under 1,000 characters.");
        RuleFor(x => x.Name).MaximumLength(100);
    }
}

public sealed class RespondToMoiJobCommandHandler(
    IApplicationDbContext context,
    IMailSender mail,
    IWhatsAppSender whatsApp,
    IBackgroundWork background,
    IOptions<MailSettings> mailOptions,
    IOptions<TwilioSettings> twilioOptions,
    TimeProvider timeProvider,
    ILogger<RespondToMoiJobCommandHandler> logger) : IRequestHandler<RespondToMoiJobCommand, ApiResult<PublicJobBillDto>>
{
    public async Task<ApiResult<PublicJobBillDto>> Handle(RespondToMoiJobCommand request, CancellationToken cancellationToken)
    {
        var token = request.Token.Trim();
        var job = token.Length < 24 ? null : await context.MoiJobs.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.BillToken == token, cancellationToken);
        if (job is null || !MoiJobBill.IsSent(job))
            return ApiResult<PublicJobBillDto>.Failure("This link is not valid.");

        // Only the bill that is actually waiting can be answered. A second tap, or a tap on an old email after the bill
        // was revised, is told so plainly instead of silently changing what was decided.
        if (job.Status == "Closed")
            return ApiResult<PublicJobBillDto>.Failure("You have already approved this bill. Thank you!");
        if (job.Status != "Sent")
            return ApiResult<PublicJobBillDto>.Failure("We have received your answer and are preparing an updated bill for you.");

        var decision = request.Decision!.Trim().ToLowerInvariant();
        var who = string.IsNullOrWhiteSpace(request.Name) ? job.CustomerName : request.Name.Trim();
        var message = MoiJobRules.Clean(request.Message);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        job.ClientResponderName = who;
        job.ClientRespondedAt = now;
        job.ClientMessage = message;
        job.UpdatedAt = now;

        string action;
        if (decision == "approve")
        {
            action = "Approved";
            job.ClientDecision = "Approved";
            job.Status = "Closed";            // the deal is final
            job.ClosedAt = now;
            job.ClosedByName = $"{who} (client)";
        }
        else
        {
            action = decision == "reject" ? "Rejected" : "Commented";
            job.ClientDecision = action;
            job.Status = "ChangesRequested";  // back to the person who issued it
        }

        context.MoiJobEvents.Add(new MoiJobEvent
        {
            Id = Guid.CreateVersion7(),
            MoiJobId = job.Id,
            Action = action,
            ActorName = $"{who} (client)",
            Note = message,
            Revision = job.BillRevision,
            AmountPayable = job.AmountPayable,
        });
        await context.SaveChangesAsync(cancellationToken);

        // The answer is saved. Telling everyone takes several seconds of mail-server conversation, so it is handed to the
        // background: the client's page answers at once instead of hanging on our emails.
        var notes = await BuildNotificationsAsync(job, action, who, message, cancellationToken);

        // A bill that reached the client on WhatsApp is answered on WhatsApp too: a thank-you with the payment details,
        // or a receipt for their rejection or comment — the same as the emails.
        var clientWhatsApp = job.WhatsAppSentAt is not null && twilioOptions.Value.IsConfigured
            ? WhatsAppNumbers.ToE164(job.CustomerNumber) : null;
        var reply = clientWhatsApp is null ? null
            : action == "Approved" ? JobBillWhatsApp.Approved(twilioOptions.Value, job.CustomerName, job.JobNumber, job.AmountPayable)
            : JobBillWhatsApp.ReplyReceived(twilioOptions.Value, job.CustomerName, job.JobNumber, rejected: action == "Rejected");

        if (notes.Count > 0 || reply is not null)
            background.Enqueue(async token =>
            {
                foreach (var note in notes)
                {
                    try { await mail.SendAsync(note, token); }
                    catch (Exception ex) { logger.LogWarning(ex, "Could not send a notification email for job {JobNumber}.", job.JobNumber); }
                }
                if (reply is not null)
                {
                    try
                    {
                        var sent = await whatsApp.SendAsync(clientWhatsApp!, reply, token);
                        if (!sent.Ok) logger.LogWarning("Could not WhatsApp the client about job {JobNumber}: {Error}", job.JobNumber, sent.Error);
                    }
                    catch (Exception ex) { logger.LogWarning(ex, "Could not WhatsApp the client about job {JobNumber}.", job.JobNumber); }
                }
            });

        return await new GetPublicJobBillQueryHandler(context).Handle(new GetPublicJobBillQuery(token), cancellationToken);
    }

    /// <summary>
    /// Who hears about the client's answer. Until the client approves, the bill is between the person who sent it and the
    /// client: a reject or comment goes to the sender (their login email and the mailbox the bill went from), and the
    /// client gets a receipt. An approval is final, so one email goes to everyone: the client, the company inbox
    /// (info@jamago.qa), the sender and the technician who did the work. Only built here (it needs the database); sent
    /// later, in the background, with best effort — the answer is already saved and staff see it in the app regardless.
    /// </summary>
    private async Task<List<MailSendRequest>> BuildNotificationsAsync(MoiJob job, string action, string who, string? message, CancellationToken ct)
    {
        var notes = new List<MailSendRequest>();
        try
        {
            var account = mailOptions.Value.Accounts.FirstOrDefault(a =>
                string.Equals(a.Key, job.SentFromAccount, StringComparison.OrdinalIgnoreCase) && a.IsConfigured);
            if (account is null) return notes;

            var senderId = job.SentById ?? job.AssignedById;
            var emails = await context.AdminUsers.AsNoTracking()
                .Where(u => u.Id == senderId || u.Id == job.AssignedToId)
                .Select(u => new { u.Id, u.Email }).ToListAsync(ct);
            var senderEmail = emails.FirstOrDefault(e => e.Id == senderId)?.Email;
            var technicianEmail = emails.FirstOrDefault(e => e.Id == job.AssignedToId)?.Email;
            var money = job.AmountPayable.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);

            static List<string> Distinct(params string?[] addresses) => addresses
                .Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (action == "Approved")
            {
                // ---- one email to everyone: the client, the company inbox, the sender and the technician
                var heading = $"Invoice {job.JobNumber} approved";
                var lines = new List<string>
                {
                    $"Dear {job.CustomerName},",
                    $"Thank you for approving invoice {job.JobNumber} for QAR {money}.",
                    $"Approved by {who} on {(job.ClientRespondedAt ?? timeProvider.GetUtcNow().UtcDateTime).AddHours(3).ToString("d MMM yyyy, h:mm tt", System.Globalization.CultureInfo.InvariantCulture)} (Doha time).",
                    "You can pay using the details below.",
                };
                var (text, html) = JobBillEmail.Note(heading, lines, CompanyPaymentDetails.AsText());
                var to = Distinct(job.CustomerEmail);
                var cc = Distinct(mailOptions.Value.CompanyInbox, senderEmail, technicianEmail)
                    .Where(a => !to.Contains(a, StringComparer.OrdinalIgnoreCase)).ToList();
                if (to.Count == 0 && cc.Count > 0) { to = [cc[0]]; cc.RemoveAt(0); }
                if (to.Count > 0)
                    notes.Add(new MailSendRequest(account.Key, "Jama Go", to, cc, $"{heading} — Jama Go Security Equipment", text, null, null, html));
                return notes;
            }

            // ---- reject or comment: the sender only…
            var staffHeading = action == "Rejected"
                ? $"{job.CustomerName} rejected invoice {job.JobNumber}"
                : $"{job.CustomerName} commented on invoice {job.JobNumber}";
            var staffLines = new List<string>
            {
                $"Invoice {job.JobNumber} · revision {job.BillRevision} · amount QAR {money}.",
                $"Answered by {who} (the client).",
            };
            if (!string.IsNullOrWhiteSpace(message)) staffLines.Add($"Their message: “{message}”");
            staffLines.Add("Open MOI Jobs, adjust the discount if needed and send a new revision.");
            var (staffText, staffHtml) = JobBillEmail.Note(staffHeading, staffLines);
            var staffTo = Distinct(senderEmail, account.Address);
            if (staffTo.Count > 0)
                notes.Add(new MailSendRequest(account.Key, "Jama Go", staffTo, [], staffHeading, staffText, null, null, staffHtml));

            // …and a receipt for the client
            if (!string.IsNullOrWhiteSpace(job.CustomerEmail))
            {
                const string clientHeading = "We have received your message";
                var clientLines = new List<string>
                {
                    $"Dear {job.CustomerName},",
                    $"We have received your {(action == "Rejected" ? "rejection" : "comment")} on invoice {job.JobNumber}.",
                    "We will look at it and send you an updated invoice soon.",
                };
                var (clientText, clientHtml) = JobBillEmail.Note(clientHeading, clientLines);
                notes.Add(new MailSendRequest(account.Key, "Jama Go", [job.CustomerEmail!], [], $"{clientHeading} — {job.JobNumber}", clientText, null, null, clientHtml));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not prepare the notification emails for job {JobNumber}.", job.JobNumber);
        }

        return notes;
    }
}
