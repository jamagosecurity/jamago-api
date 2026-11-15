namespace Jama.Application.Mail;

public sealed record MailListItemDto(
    Guid Id, string AccountKey, string AccountAddress, string Folder,
    string FromName, string FromAddress, string ToAddresses, string Subject,
    DateTime SentAt, string Snippet, bool HasAttachments, bool IsRead = true);

public sealed record MailDetailDto(
    Guid Id, string AccountKey, string AccountAddress, string Folder,
    string FromName, string FromAddress, string ToAddresses, string Subject,
    DateTime SentAt, string BodyText, string? BodyHtml, bool HasAttachments, string AttachmentNames, bool IsRead = true);

public sealed record MailPageDto(IReadOnlyList<MailListItemDto> Items, int Total);

public sealed record MailAccountDto(
    string Key, string Address, bool Configured, int InboxCount, int SentCount,
    DateTime? LastSyncAt, string? LastError, string Signature = "",
    /// <summary>The signature card as sent (the logo as cid:jamago-logo), for the compose preview.</summary>
    string SignatureHtml = "", string SignerName = "",
    /// <summary>Received messages not read yet.</summary>
    int UnreadCount = 0,
    /// <summary>No password here, but its incoming mail arrives through the copy mailbox.</summary>
    bool ViaCopy = false);
