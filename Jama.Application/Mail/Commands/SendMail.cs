using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Application.Options;
using Jama.Domain.Entities;
using Jama.Application.Mail;
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
    Guid? ReplyToMessageId,
    /// <summary>The original message under a reply or forward; shown below the signature.</summary>
    string? Quoted = null,
    /// <summary>Hidden recipients: they get the message, nobody else sees them.</summary>
    IReadOnlyList<string>? Bcc = null,
    /// <summary>The formatted message from the editor (bold, lists, links). Cleaned before sending; <see cref="Body"/> stays the plain-text version.</summary>
    string? BodyHtml = null,
    IReadOnlyList<SendMailAttachment>? Attachments = null) : IRequest<ApiResult<MailSentDto>>;

/// <summary>A file attached in the editor, sent as base64.</summary>
public sealed record SendMailAttachment(string? FileName, string? ContentType, string? Base64);

public sealed record MailSentDto(bool CopySavedToSent, Guid? SentMessageId);

public sealed class SendMailCommandValidator : AbstractValidator<SendMailCommand>
{
    public const int MaxRecipients = 20;
    public const int MaxAttachments = 10;
    public const long MaxAttachmentBytes = 20L * 1024 * 1024;
    private static readonly HashSet<string> Blocked = [".exe", ".bat", ".cmd", ".com", ".scr", ".js", ".vbs", ".ps1", ".msi", ".jar", ".reg", ".hta"];

    public SendMailCommandValidator()
    {
        RuleFor(x => x.AccountKey).NotEmpty().WithMessage("Choose which mailbox to send from.");
        RuleFor(x => x.To).NotNull().Must(t => t is { Count: > 0 }).WithMessage("Add at least one recipient.");
        RuleFor(x => x.To).Must(t => t is null || t.Count <= MaxRecipients)
            .WithMessage($"At most {MaxRecipients} recipients.");
        RuleForEach(x => x.To).Must(IsAddress).WithMessage("\"{PropertyValue}\" is not a valid email address.");
        RuleForEach(x => x.Cc).Must(IsAddress).WithMessage("\"{PropertyValue}\" is not a valid email address.");
        RuleFor(x => x.Cc).Must(c => c is null || c.Count <= MaxRecipients).WithMessage($"At most {MaxRecipients} Cc recipients.");
        RuleForEach(x => x.Bcc).Must(IsAddress).WithMessage("\"{PropertyValue}\" is not a valid email address.");
        RuleFor(x => x.Bcc).Must(c => c is null || c.Count <= MaxRecipients).WithMessage($"At most {MaxRecipients} Bcc recipients.");
        RuleFor(x => x.BodyHtml).MaximumLength(300_000).WithMessage("The message is too long.");
        RuleFor(x => x.Attachments).Must(a => a is null || a.Count <= MaxAttachments).WithMessage($"At most {MaxAttachments} attachments.");
        RuleFor(x => x.Attachments).Must(a => a is null || a.Sum(f => (long)((f.Base64?.Length ?? 0) * 3L / 4)) <= MaxAttachmentBytes)
            .WithMessage("Attachments can be 20 MB in total at most.");
        RuleForEach(x => x.Attachments).Must(f => !string.IsNullOrWhiteSpace(f.FileName) && !string.IsNullOrWhiteSpace(f.Base64))
            .WithMessage("An attachment is empty.");
        RuleForEach(x => x.Attachments).Must(f => !Blocked.Contains(Path.GetExtension(f.FileName ?? "").ToLowerInvariant()))
            .WithMessage("Programs and scripts (.exe, .bat, .js …) cannot be attached — mail servers reject them.");
        RuleFor(x => x.Subject).NotEmpty().WithMessage("Write a subject.")
            .MaximumLength(300).WithMessage("The subject must be 300 characters or fewer.")
            // A line break in a header is how header-injection attacks smuggle in extra recipients.
            .Must(s => s is null || !s.Contains('\r') && !s.Contains('\n')).WithMessage("The subject must be one line.");
        RuleFor(x => x.Body).NotEmpty().WithMessage("Write a message.")
            .MaximumLength(100_000).WithMessage("The message is too long.");
        RuleFor(x => x.Quoted).MaximumLength(200_000).WithMessage("The quoted message is too long.");
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
        var bcc = (request.Bcc ?? []).Select(a => a.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(a => !to.Contains(a, StringComparer.OrdinalIgnoreCase) && !cc.Contains(a, StringComparer.OrdinalIgnoreCase)).ToList();

        var files = new List<MailAttachment>();
        foreach (var f in request.Attachments ?? [])
        {
            byte[] bytes;
            try { bytes = Convert.FromBase64String(f.Base64!); }
            catch (FormatException) { return ApiResult<MailSentDto>.Failure($"The attachment \"{f.FileName}\" could not be read."); }
            var name = Path.GetFileName(f.FileName!).Replace("\r", "").Replace("\n", "").Trim();
            var type = string.IsNullOrWhiteSpace(f.ContentType) || f.ContentType.Contains('\n') ? "application/octet-stream" : f.ContentType.Trim();
            files.Add(new MailAttachment(name.Length == 0 ? "attachment" : name, type, bytes));
        }
        var subject = request.Subject!.Trim();

        // The message as written, the mailbox's signature, then the original (reply / forward) underneath.
        var body = request.Body!.TrimEnd();
        var quoted = string.IsNullOrWhiteSpace(request.Quoted) ? null : request.Quoted.Trim();
        var card = await EmailSignature.ResolveAsync(context, account, cancellationToken);
        var text = body + "\n\n" + EmailSignature.Text(account, card) + (quoted is null ? "" : "\n\n" + quoted);
        var formatted = string.IsNullOrWhiteSpace(request.BodyHtml) ? null : MailHtml.Clean(request.BodyHtml);
        var html = MessageHtml(body, EmailSignature.Html(account, card), quoted, formatted);
        var signer = card.Name;

        // Our own Message-ID: the copy shown under Outgoing now and the one the Sent folder gets later are the same message.
        var domain = account.Address.Contains('@') ? account.Address[(account.Address.IndexOf('@') + 1)..] : "jamago.qa";
        var messageId = $"{Guid.CreateVersion7():N}@{domain}";

        var result = await sender.SendAsync(
            new MailSendRequest(account.Key, signer, to, cc, subject, text, inReplyTo, files.Count > 0 ? files : null, html, bcc,
                MessageId: messageId, SaveCopyInBackground: true), cancellationToken);
        if (!result.Ok)
            return ApiResult<MailSentDto>.Failure(result.Error ?? "The message could not be sent.");

        // Saved now from our side; the mailbox's own Sent folder gets its copy in the background a moment later.
        var copySaved = true;
        context.MailSendLogs.Add(new MailSendLog
        {
            Id = Guid.CreateVersion7(),
            AccountKey = account.Key,
            FromAddress = account.Address,
            ToAddresses = string.Join(", ", to.Concat(cc).Concat(bcc.Select(b => "bcc:" + b))),
            Subject = subject.Length > 500 ? subject[..500] : subject,
            SentById = actor.UserId,
            SentByName = actor.DisplayName,
            ReplyToMessageId = request.ReplyToMessageId,
            CopySavedToSent = copySaved,
        });

        // Show the message under Outgoing straight away. It carries the same UID the Sent folder gave it,
        // so the next sync recognises it and does not add it a second time.
        Guid? sentId = null;
        {
            sentId = Guid.CreateVersion7();
            var snippet = string.Join(' ', body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            context.MailMessages.Add(new MailMessage
            {
                Id = sentId.Value,
                AccountKey = account.Key,
                AccountAddress = account.Address,
                SourceKey = account.Key,
                Folder = "Sent",
                // The Sent folder's real UID is not known yet; a unique negative stand-in keeps the row apart, and the
                // sync matches the real copy to this one by Message-ID and does not add it twice.
                UidValidity = result.CopyUidValidity ?? 0,
                Uid = result.CopyUid ?? -DateTime.UtcNow.Ticks,
                MessageId = messageId,
                FromName = signer,
                FromAddress = account.Address,
                ToAddresses = string.Join(", ", to),
                Subject = subject,
                SentAt = DateTime.UtcNow,
                Snippet = snippet.Length > 240 ? snippet[..240] : snippet,
                BodyText = text,
                BodyHtml = html,
                HasAttachments = files.Count > 0,
                AttachmentNames = string.Join("\n", files.Select(f => f.FileName)),
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        return ApiResult<MailSentDto>.Success(new MailSentDto(copySaved, sentId));
    }

    private static string MessageHtml(string body, string signature, string? quoted, string? formatted = null)
    {
        static string Paragraphs(string value) =>
            System.Net.WebUtility.HtmlEncode(value.Replace("\r", string.Empty)).Replace("\n", "<br>");

        var quote = quoted is null ? string.Empty
            : $@"<div style=""margin-top:26px;padding:12px 0 0 14px;border-left:3px solid #E3E0D8;font-family:'Segoe UI',Helvetica,Arial,sans-serif;font-size:13px;line-height:1.6;color:#4B5260"">{Paragraphs(quoted)}</div>";
        return $@"<!doctype html><html><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""></head>
<body style=""margin:0;padding:16px;background:#ffffff"">
<div style=""font-family:'Segoe UI',Helvetica,Arial,sans-serif;font-size:14px;line-height:1.65;color:#0B1730"">{formatted ?? Paragraphs(body)}</div>
{signature}{quote}</body></html>";
    }
}
