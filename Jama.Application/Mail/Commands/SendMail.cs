using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Application.Options;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jama.Application.Mail.Commands;

/// <summary>Send a new message, a reply or a forward from one of the company mailboxes.
/// Administrators only (checked at the endpoint); every send is written to the send log.</summary>
public sealed record SendMailCommand(
    string? AccountKey,
    IReadOnlyList<string>? To,
    IReadOnlyList<string>? Cc,
    string? Subject,
    string? Body,
    Guid? ReplyToMessageId) : IRequest<ApiResult<MailSentDto>>;

public sealed record MailSentDto(bool CopySavedToSent, Guid? SentMessageId);

public sealed class SendMailCommandValidator : AbstractValidator<SendMailCommand>
{
    public const int MaxRecipients = 20;

    public SendMailCommandValidator()
    {
        RuleFor(x => x.AccountKey).NotEmpty().WithMessage("Choose which mailbox to send from.");
        RuleFor(x => x.To).NotNull().Must(t => t is { Count: > 0 }).WithMessage("Add at least one recipient.");
        RuleFor(x => x.To).Must(t => t is null || t.Count <= MaxRecipients)
            .WithMessage($"At most {MaxRecipients} recipients.");
        RuleForEach(x => x.To).Must(IsAddress).WithMessage("\"{PropertyValue}\" is not a valid email address.");
        RuleForEach(x => x.Cc).Must(IsAddress).WithMessage("\"{PropertyValue}\" is not a valid email address.");
        RuleFor(x => x.Cc).Must(c => c is null || c.Count <= MaxRecipients).WithMessage($"At most {MaxRecipients} Cc recipients.");
        RuleFor(x => x.Subject).NotEmpty().WithMessage("Write a subject.")
            .MaximumLength(300).WithMessage("The subject must be 300 characters or fewer.")
            // A line break in a header is how header-injection attacks smuggle in extra recipients.
            .Must(s => s is null || !s.Contains('\r') && !s.Contains('\n')).WithMessage("The subject must be one line.");
        RuleFor(x => x.Body).NotEmpty().WithMessage("Write a message.")
            .MaximumLength(100_000).WithMessage("The message is too long.");
    }

    /// <summary>Plain, single address: name@domain.tld, no spaces, no angle brackets or line breaks.</summary>
    internal static bool IsAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 320) return false;
        var v = value.Trim();
        if (v.IndexOfAny(['\r', '\n', ' ', '<', '>', ',', ';']) >= 0) return false;
        var at = v.IndexOf('@');
        return at > 0 && at == v.LastIndexOf('@') && v[(at + 1)..].Contains('.') && !v.EndsWith('.') && !v[(at + 1)..].StartsWith('.');
    }
}

public sealed class SendMailCommandHandler(
    IApplicationDbContext context,
    IMailSender sender,
    ICurrentUser actor,
    IOptions<MailSettings> options) : IRequestHandler<SendMailCommand, ApiResult<MailSentDto>>
{
    public async Task<ApiResult<MailSentDto>> Handle(SendMailCommand request, CancellationToken cancellationToken)
    {
        var account = options.Value.Accounts.FirstOrDefault(a =>
            string.Equals(a.Key, request.AccountKey, StringComparison.OrdinalIgnoreCase));
        if (account is null || !account.IsConfigured)
            return ApiResult<MailSentDto>.Failure("That mailbox is not set up for sending.");

        string? inReplyTo = null;
        if (request.ReplyToMessageId is { } replyId)
        {
            var original = await context.MailMessages.AsNoTracking()
                .Where(m => m.Id == replyId).Select(m => m.MessageId).FirstOrDefaultAsync(cancellationToken);
            inReplyTo = original;
        }

        var to = request.To!.Select(a => a.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var cc = (request.Cc ?? []).Select(a => a.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(a => !to.Contains(a, StringComparer.OrdinalIgnoreCase)).ToList();
        var subject = request.Subject!.Trim();

        var result = await sender.SendAsync(
            new MailSendRequest(account.Key, "Jama Go", to, cc, subject, request.Body!, inReplyTo), cancellationToken);
        if (!result.Ok)
            return ApiResult<MailSentDto>.Failure(result.Error ?? "The message could not be sent.");

        var copySaved = result.CopyUid is not null;
        context.MailSendLogs.Add(new MailSendLog
        {
            Id = Guid.CreateVersion7(),
            AccountKey = account.Key,
            FromAddress = account.Address,
            ToAddresses = string.Join(", ", to.Concat(cc)),
            Subject = subject.Length > 500 ? subject[..500] : subject,
            SentById = actor.UserId,
            SentByName = actor.DisplayName,
            ReplyToMessageId = request.ReplyToMessageId,
            CopySavedToSent = copySaved,
        });

        // Show the message under Outgoing straight away. It carries the same UID the Sent folder gave it,
        // so the next sync recognises it and does not add it a second time.
        Guid? sentId = null;
        if (copySaved)
        {
            var body = request.Body!;
            sentId = Guid.CreateVersion7();
            context.MailMessages.Add(new MailMessage
            {
                Id = sentId.Value,
                AccountKey = account.Key,
                AccountAddress = account.Address,
                Folder = "Sent",
                UidValidity = result.CopyUidValidity!.Value,
                Uid = result.CopyUid!.Value,
                FromName = "Jama Go",
                FromAddress = account.Address,
                ToAddresses = string.Join(", ", to),
                Subject = subject,
                SentAt = DateTime.UtcNow,
                Snippet = string.Join(' ', body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) is { Length: > 240 } s ? s[..240] : string.Join(' ', body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)),
                BodyText = body,
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        return ApiResult<MailSentDto>.Success(new MailSentDto(copySaved, sentId));
    }
}
