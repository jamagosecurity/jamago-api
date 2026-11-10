namespace Jama.Application.Common.Interfaces;

/// <summary>A file attached to an outgoing message.</summary>
public sealed record MailAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>What the mail sender needs. Addresses are already syntax-checked by the command validator.</summary>
public sealed record MailSendRequest(
    string AccountKey,
    string FromDisplayName,
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    string Subject,
    string Body,
    string? InReplyToMessageId,
    IReadOnlyList<MailAttachment>? Attachments = null,
    /// <summary>When set the message goes out as HTML with <see cref="Body"/> as its plain-text alternative.</summary>
    string? HtmlBody = null);

/// <summary>Outcome of one send. <see cref="CopyUid"/> is set when a copy reached the mailbox's Sent folder.</summary>
public sealed record MailSendResult(bool Ok, string? Error, long? CopyUidValidity = null, long? CopyUid = null);

/// <summary>Sends mail through a company mailbox's own outgoing server.</summary>
public interface IMailSender
{
    Task<MailSendResult> SendAsync(MailSendRequest request, CancellationToken cancellationToken);
}
