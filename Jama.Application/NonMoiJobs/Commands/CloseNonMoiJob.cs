using System.Security.Cryptography;
using FluentValidation;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Application.Options;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jama.Application.NonMoiJobs.Commands;

/// <summary>
/// The person who issued the job reviews what the technician did, may take a discount off the total (nothing else
/// about the prices can change), and SENDS THE BILL TO THE CLIENT for approval — by email and WhatsApp, each carrying a link
/// to the client's own page with Approve / Reject / Comment. The job becomes "Sent" and locks while it is with the client.
/// If the client rejects or comments, it comes back here ("ChangesRequested") and can be adjusted and sent again as the
/// next revision. The deal is Closed only when the client approves.
///
/// Sending is not all-or-nothing in one breath. Each channel that goes out is recorded at once, so if the second one
/// fails and the person tries again, only the one that has not gone out is sent — the client never gets the same email twice.
/// </summary>
public sealed record CloseNonMoiJobCommand(
    Guid Id,
    decimal Discount,
    string? Email,
    bool SendEmail,
    bool SendWhatsApp,
    string? MailboxKey) : IRequest<ApiResult<NonMoiJobDto>>;

public sealed class CloseNonMoiJobCommandValidator : AbstractValidator<CloseNonMoiJobCommand>
{
    public const decimal DiscountMax = 9_999_999.99m;

    public CloseNonMoiJobCommandValidator()
    {
        RuleFor(x => x.Discount)
            .GreaterThanOrEqualTo(0m).WithMessage("The discount cannot be negative.")
            .LessThanOrEqualTo(DiscountMax).WithMessage($"The discount must be {DiscountMax:N2} or less.");
        RuleFor(x => x).Must(x => x.SendEmail || x.SendWhatsApp)
            .WithMessage("Choose at least one way to send the bill: email or WhatsApp.")
            .OverridePropertyName("Channels");
        RuleFor(x => x.MailboxKey).NotEmpty().WithMessage("Choose which company mailbox the email is sent from.")
            .When(x => x.SendEmail);
        RuleFor(x => x.Email)
            .Must(e => string.IsNullOrWhiteSpace(e) || Jama.Application.Mail.Commands.SendMailCommandValidator.IsAddress(e))
            .WithMessage("Enter a valid email address, like name@example.com.");
    }
}

public sealed class CloseNonMoiJobCommandHandler(
    IApplicationDbContext context,
    ICurrentUser actor,
    IMailSender mail,
    IWhatsAppSender whatsApp,
    IJobBillPdfGenerator billPdf,
    IOptions<MailSettings> mailOptions,
    IOptions<TwilioSettings> twilioOptions,
    TimeProvider timeProvider) : IRequestHandler<CloseNonMoiJobCommand, ApiResult<NonMoiJobDto>>
{
    public async Task<ApiResult<NonMoiJobDto>> Handle(CloseNonMoiJobCommand request, CancellationToken cancellationToken)
    {
        var job = await context.NonMoiJobs.Include(x => x.Lines).Include(x => x.Events)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (job is null)
            return ApiResult<NonMoiJobDto>.Failure("Job not found.");

        // The person who issued it, or an administrator. Anyone else with the grant can look but not send.
        var isAdmin = actor.Role == Roles.Admin;
        if (!isAdmin && (job.AssignedById == Guid.Empty || job.AssignedById != actor.UserId))
            return ApiResult<NonMoiJobDto>.Failure("Only the person who issued this job, or an administrator, can send its bill.");

        if (job.Status == "Closed")
            return ApiResult<NonMoiJobDto>.Failure("The client has already approved this job.");
        if (job.Status == "Sent")
            return ApiResult<NonMoiJobDto>.Failure("This bill is already with the client — waiting for their answer.");
        if (job.Status is not ("Completed" or "ChangesRequested"))
            return ApiResult<NonMoiJobDto>.Failure("The technician has not completed this job yet.");
        if (request.Discount > job.WorkTotal)
            return ApiResult<NonMoiJobDto>.Failure("The discount cannot be more than the total.");

        var email = NonMoiJobRules.Clean(request.Email) ?? job.CustomerEmail;
        MailAccountSettings? account = null;
        if (request.SendEmail)
        {
            if (string.IsNullOrWhiteSpace(email))
                return ApiResult<NonMoiJobDto>.Failure("Add the client's email address, or untick the email.");
            account = mailOptions.Value.Accounts.FirstOrDefault(a =>
                string.Equals(a.Key, request.MailboxKey, StringComparison.OrdinalIgnoreCase));
            if (account is null || !account.IsConfigured)
                return ApiResult<NonMoiJobDto>.Failure("That mailbox is not set up for sending.");
        }
        if (request.SendWhatsApp && !twilioOptions.Value.IsConfigured)
            return ApiResult<NonMoiJobDto>.Failure("WhatsApp is not set up on this server. Untick it, or ask the administrator to set it up.");

        // The client's earlier answer has been read: this send is a new revision of the bill. Done once — a retry after a
        // half-failed send finds ClientDecision already cleared and does not count a second revision.
        if (job.Status == "ChangesRequested" && job.ClientDecision is not null)
        {
            job.BillRevision += 1;
            job.ClientDecision = null;
            job.EmailSentAt = null;
            job.WhatsAppSentAt = null;
        }

        // What the client will see is fixed here, before anything goes out: the discount, the email address, and the
        // link token. If a send fails the person can retry and the bill will be exactly the same.
        job.Discount = request.Discount;
        job.AmountPayable = job.WorkTotal - request.Discount;
        job.CustomerEmail = email;
        job.SentFromAccount = request.SendEmail ? account!.Key : job.SentFromAccount;
        job.BillToken ??= Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        await context.SaveChangesAsync(cancellationToken);

        var pageUrl = $"{mailOptions.Value.PublicSiteUrl.TrimEnd('/')}/bill/{job.BillToken}";

        if (request.SendEmail && job.EmailSentAt is null)
        {
            var pdf = billPdf.Generate(NonMoiJobBill.ToModel(job));
            var (subject, text, html) = JobBillEmail.Bill(job, pageUrl, account!.Signature);

            var sent = await mail.SendAsync(
                new MailSendRequest(account.Key, "Jama Go", [email!], [], subject, text, null,
                    [new MailAttachment(NonMoiJobBill.FileName(job), "application/pdf", pdf)], html),
                cancellationToken);

            if (!sent.Ok)
                return ApiResult<NonMoiJobDto>.Failure(sent.Error ?? "The email could not be sent.");

            job.EmailSentAt = timeProvider.GetUtcNow().UtcDateTime;
            context.MailSendLogs.Add(new MailSendLog
            {
                Id = Guid.CreateVersion7(),
                AccountKey = account.Key,
                FromAddress = account.Address,
                ToAddresses = email!,
                Subject = subject,
                SentById = actor.UserId,
                SentByName = actor.DisplayName,
                CopySavedToSent = sent.CopyUid is not null,
            });
            await context.SaveChangesAsync(cancellationToken);
        }

        if (request.SendWhatsApp && job.WhatsAppSentAt is null)
        {
            var sent = await whatsApp.SendJobBillAsync(
                job.CustomerNumber, job.CustomerName, job.JobNumber, job.AmountPayable, pageUrl, cancellationToken);
            if (!sent.Ok)
            {
                var already = job.EmailSentAt is not null ? " The email has already gone out, so trying again will only resend the WhatsApp message." : string.Empty;
                return ApiResult<NonMoiJobDto>.Failure((sent.Error ?? "The WhatsApp message could not be sent.") + already);
            }

            job.WhatsAppSentAt = timeProvider.GetUtcNow().UtcDateTime;
            await context.SaveChangesAsync(cancellationToken);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        job.Status = "Sent";
        job.ClientMessage = null;
        job.UpdatedAt = now;
        context.NonMoiJobEvents.Add(new NonMoiJobEvent
        {
            Id = Guid.CreateVersion7(),
            NonMoiJobId = job.Id,
            Action = "Sent",
            ActorName = actor.DisplayName,
            Note = string.Join(" + ", new[] { request.SendEmail ? "email" : null, request.SendWhatsApp ? "WhatsApp" : null }.Where(x => x is not null)),
            Revision = job.BillRevision,
            AmountPayable = job.AmountPayable,
        });
        await context.SaveChangesAsync(cancellationToken);

        var saved = await context.NonMoiJobs.AsNoTracking().Include(x => x.Lines).Include(x => x.Events).FirstAsync(x => x.Id == job.Id, cancellationToken);
        return ApiResult<NonMoiJobDto>.Success(NonMoiJobRules.ToDto(saved));
    }
}
