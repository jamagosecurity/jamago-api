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

namespace Jama.Application.MoiJobs.Commands;

/// <summary>
/// A MOI manager (usually the person who issued the job) reviews what the technician did, may take a discount off the total (nothing else
/// about the prices can change), and SENDS THE BILL TO THE CLIENT for approval — by email and WhatsApp, each carrying a link
/// to the client's own page with Approve / Reject / Comment. The job becomes "Sent" and locks while it is with the client.
/// If the client rejects or comments, it comes back here ("ChangesRequested") and can be adjusted and sent again as the
/// next revision. The deal is Closed only when the client approves.
///
/// Sending is not all-or-nothing in one breath. Each channel that goes out is recorded at once, so if the second one
/// fails and the person tries again, only the one that has not gone out is sent — the client never gets the same email twice.
/// </summary>
public sealed record CloseMoiJobCommand(
    Guid Id,
    decimal Discount,
    string? Email,
    bool SendEmail,
    bool SendWhatsApp,
    string? MailboxKey) : IRequest<ApiResult<MoiJobDto>>;

public sealed class CloseMoiJobCommandValidator : AbstractValidator<CloseMoiJobCommand>
{
    public const decimal DiscountMax = 9_999_999.99m;

    public CloseMoiJobCommandValidator()
    {
        RuleFor(x => x.Discount)
            .GreaterThanOrEqualTo(0m).WithMessage("The discount cannot be negative.")
            .LessThanOrEqualTo(DiscountMax).WithMessage($"The discount must be {DiscountMax:N2} or less.");
        // The bill goes to the client by email, always. WhatsApp is not used for bills.
        RuleFor(x => x.SendEmail).Equal(true)
            .WithMessage("The bill is sent to the client by email, so email cannot be turned off.")
            .OverridePropertyName("Channels");
        RuleFor(x => x.MailboxKey).NotEmpty().WithMessage("Choose which company mailbox the email is sent from.");
        RuleFor(x => x.Email)
            .Must(e => string.IsNullOrWhiteSpace(e) || Jama.Application.Mail.Commands.SendMailCommandValidator.IsAddress(e))
            .WithMessage("Enter a valid email address, like name@example.com.");
    }
}

public sealed class CloseMoiJobCommandHandler(
    IApplicationDbContext context,
    ICurrentUser actor,
    IMailSender mail,
    IWhatsAppSender whatsApp,
    IJobBillPdfGenerator billPdf,
    IOptions<MailSettings> mailOptions,
    IOptions<TwilioSettings> twilioOptions,
    TimeProvider timeProvider) : IRequestHandler<CloseMoiJobCommand, ApiResult<MoiJobDto>>
{
    public async Task<ApiResult<MoiJobDto>> Handle(CloseMoiJobCommand request, CancellationToken cancellationToken)
    {
        var job = await context.MoiJobs.Include(x => x.Lines).Include(x => x.Events).Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (job is null)
            return ApiResult<MoiJobDto>.Failure("Job not found.");

        // Email is mandatory; WhatsApp (SendWhatsApp) is accepted for compatibility but never used — nothing goes out through it.
        if (!request.SendEmail)
            return ApiResult<MoiJobDto>.Failure("The bill is sent to the client by email, so email cannot be turned off.");

        // Anyone who reaches this handler holds the MOI manage permission (the endpoint requires it), so they may review
        // and send the bill — not only the person who issued the job. A job is often issued by the administrator and billed by
        // the MOI staff member; the issuer is still told when the client answers.

        if (job.Status == "Closed")
            return ApiResult<MoiJobDto>.Failure("The client has already approved this job.");
        if (job.Status == "Sent")
            return ApiResult<MoiJobDto>.Failure("This bill is already with the client — waiting for their answer.");
        if (job.Status is not ("Completed" or "ChangesRequested"))
            return ApiResult<MoiJobDto>.Failure("The technician has not completed this job yet.");
        if (request.Discount > job.WorkTotal)
            return ApiResult<MoiJobDto>.Failure("The discount cannot be more than the total.");

        var email = MoiJobRules.Clean(request.Email) ?? job.CustomerEmail;
        MailAccountSettings? account = null;
        if (request.SendEmail)
        {
            if (string.IsNullOrWhiteSpace(email))
                return ApiResult<MoiJobDto>.Failure("Add the client's email address — the bill is sent by email.");
            account = mailOptions.Value.Accounts.FirstOrDefault(a =>
                string.Equals(a.Key, request.MailboxKey, StringComparison.OrdinalIgnoreCase));
            if (account is null || !account.IsConfigured)
                return ApiResult<MoiJobDto>.Failure("That mailbox is not set up for sending.");
        }

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
        job.SentById = actor.UserId;
        job.BillToken ??= Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        await context.SaveChangesAsync(cancellationToken);

        var pageUrl = $"{mailOptions.Value.PublicSiteUrl.TrimEnd('/')}/moi-bill/{job.BillToken}";

        if (request.SendEmail && job.EmailSentAt is null)
        {
            var pdf = billPdf.Generate(MoiJobBill.ToModel(job));
            var card = await Jama.Application.Mail.EmailSignature.ResolveAsync(context, account!, cancellationToken);
            var (subject, text, html) = JobBillEmail.Bill(job, pageUrl, account, timeProvider.GetUtcNow().UtcDateTime, card);

            var sent = await mail.SendAsync(
                new MailSendRequest(account.Key, "Jama Go", [email!], [], subject, text, null,
                    [new MailAttachment(MoiJobBill.FileName(job), "application/pdf", pdf)], html),
                cancellationToken);

            if (!sent.Ok)
                return ApiResult<MoiJobDto>.Failure(sent.Error ?? "The email could not be sent.");

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

        var now = timeProvider.GetUtcNow().UtcDateTime;
        job.Status = "Sent";
        job.ClientMessage = null;
        job.UpdatedAt = now;
        context.MoiJobEvents.Add(new MoiJobEvent
        {
            Id = Guid.CreateVersion7(),
            MoiJobId = job.Id,
            Action = "Sent",
            ActorName = actor.DisplayName,
            Note = "email",
            Revision = job.BillRevision,
            AmountPayable = job.AmountPayable,
        });
        await context.SaveChangesAsync(cancellationToken);

        var saved = await context.MoiJobs.AsNoTracking().Include(x => x.Lines).Include(x => x.Events).Include(x => x.Payments).FirstAsync(x => x.Id == job.Id, cancellationToken);
        return ApiResult<MoiJobDto>.Success(MoiJobRules.ToDto(saved));
    }
}
