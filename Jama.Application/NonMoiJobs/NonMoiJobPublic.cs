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

namespace Jama.Application.NonMoiJobs;

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
    DateTime? DecidedAt);

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

        var job = await context.NonMoiJobs.AsNoTracking().Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.BillToken == token, cancellationToken);
        if (job is null || !NonMoiJobBill.IsSent(job))
            return ApiResult<PublicJobBillDto>.Failure("This link is not valid.");

        return ApiResult<PublicJobBillDto>.Success(new PublicJobBillDto(
            job.JobNumber, job.CustomerName, job.Location, job.BillRevision, job.Status,
            job.Lines.OrderBy(l => l.SortOrder)
                .Select(l => new PublicBillLineDto(l.IssueType, MaintenanceCategories.Label(l.Category), l.Quantity, l.UnitFee, l.LineTotal)).ToList(),
            job.WorkTotal, job.Discount, job.AmountPayable,
            CompanyPaymentDetails.AccountName, CompanyPaymentDetails.CrNumber, CompanyPaymentDetails.Iban,
            job.Status == "Sent", job.ClientDecision, job.ClientMessage, job.ClientRespondedAt));
    }
}

/// <summary>The client's answer: approve, reject (with a reason) or comment (asking for a change).</summary>
public sealed record RespondToNonMoiJobCommand(string Token, string? Decision, string? Message, string? Name)
    : IRequest<ApiResult<PublicJobBillDto>>;

public sealed class RespondToNonMoiJobCommandValidator : AbstractValidator<RespondToNonMoiJobCommand>
{
    public RespondToNonMoiJobCommandValidator()
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

public sealed class RespondToNonMoiJobCommandHandler(
    IApplicationDbContext context,
    IMailSender mail,
    IBackgroundWork background,
    IOptions<MailSettings> mailOptions,
    TimeProvider timeProvider,
    ILogger<RespondToNonMoiJobCommandHandler> logger) : IRequestHandler<RespondToNonMoiJobCommand, ApiResult<PublicJobBillDto>>
{
    public async Task<ApiResult<PublicJobBillDto>> Handle(RespondToNonMoiJobCommand request, CancellationToken cancellationToken)
    {
        var token = request.Token.Trim();
        var job = token.Length < 24 ? null : await context.NonMoiJobs.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.BillToken == token, cancellationToken);
        if (job is null || !NonMoiJobBill.IsSent(job))
            return ApiResult<PublicJobBillDto>.Failure("This link is not valid.");

        // Only the bill that is actually waiting can be answered. A second tap, or a tap on an old email after the bill
        // was revised, is told so plainly instead of silently changing what was decided.
        if (job.Status == "Closed")
            return ApiResult<PublicJobBillDto>.Failure("You have already approved this bill. Thank you!");
        if (job.Status != "Sent")
            return ApiResult<PublicJobBillDto>.Failure("We have received your answer and are preparing an updated bill for you.");

        var decision = request.Decision!.Trim().ToLowerInvariant();
        var who = string.IsNullOrWhiteSpace(request.Name) ? job.CustomerName : request.Name.Trim();
        var message = NonMoiJobRules.Clean(request.Message);
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

        context.NonMoiJobEvents.Add(new NonMoiJobEvent
        {
            Id = Guid.CreateVersion7(),
            NonMoiJobId = job.Id,
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
        if (notes.Count > 0)
            background.Enqueue(async token =>
            {
                foreach (var note in notes)
                {
                    try { await mail.SendAsync(note, token); }
                    catch (Exception ex) { logger.LogWarning(ex, "Could not send a notification email for job {JobNumber}.", job.JobNumber); }
                }
            });

        return await new GetPublicJobBillQueryHandler(context).Handle(new GetPublicJobBillQuery(token), cancellationToken);
    }

    /// <summary>The emails that tell everyone concerned: the company mailbox and the person who issued the job, and a receipt for the
    /// client. Only built here (it needs the database); sent later, in the background, with best effort — the answer is already saved, and
    /// staff see the result in the app regardless (red number, status, history).</summary>
    private async Task<List<MailSendRequest>> BuildNotificationsAsync(NonMoiJob job, string action, string who, string? message, CancellationToken ct)
    {
        var notes = new List<MailSendRequest>();
        try
        {
            var account = mailOptions.Value.Accounts.FirstOrDefault(a =>
                string.Equals(a.Key, job.SentFromAccount, StringComparison.OrdinalIgnoreCase) && a.IsConfigured);
            if (account is null) return notes;

            var issuerEmail = await context.AdminUsers.AsNoTracking()
                .Where(u => u.Id == job.AssignedById).Select(u => u.Email).FirstOrDefaultAsync(ct);
            var money = job.AmountPayable.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);

            // ---- to the company mailbox and the person who issued the job
            var staffHeading = action switch
            {
                "Approved" => $"{job.CustomerName} approved bill {job.JobNumber}",
                "Rejected" => $"{job.CustomerName} rejected bill {job.JobNumber}",
                _ => $"{job.CustomerName} commented on bill {job.JobNumber}",
            };
            var staffLines = new List<string>
            {
                $"Job {job.JobNumber} · revision {job.BillRevision} · amount payable QAR {money}.",
                $"Answered by {who} (the client).",
            };
            if (!string.IsNullOrWhiteSpace(message)) staffLines.Add($"Their message: “{message}”");
            staffLines.Add(action == "Approved"
                ? "The deal is final. Nothing more is needed in the app."
                : "The job is back with the person who issued it: open Non-MOI Jobs, adjust the discount if needed and send a new revision.");
            var (staffText, staffHtml) = JobBillEmail.Note(staffHeading, staffLines);

            var staffTo = new[] { account.Address, issuerEmail }
                .Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (staffTo.Count > 0)
                notes.Add(new MailSendRequest(account.Key, "Jama Go", staffTo, [], staffHeading, staffText, null, null, staffHtml));

            // ---- to the client: a receipt of their answer
            if (!string.IsNullOrWhiteSpace(job.CustomerEmail))
            {
                var clientHeading = action == "Approved" ? "Thank you — your bill is approved" : "We have received your message";
                var clientLines = action == "Approved"
                    ? new List<string> { $"Dear {job.CustomerName},", $"Thank you for approving bill {job.JobNumber} for QAR {money}.", "You can pay using the details below." }
                    : new List<string> { $"Dear {job.CustomerName},", $"We have received your {(action == "Rejected" ? "rejection" : "comment")} on bill {job.JobNumber}.", "We will look at it and send you an updated bill soon." };
                var (clientText, clientHtml) = JobBillEmail.Note(clientHeading, clientLines, action == "Approved" ? CompanyPaymentDetails.AsText() : null);
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
