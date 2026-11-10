namespace Jama.Application.Mail;

public sealed record MailListItemDto(
    Guid Id, string AccountKey, string AccountAddress, string Folder,
    string FromName, string FromAddress, string ToAddresses, string Subject,
    DateTime SentAt, string Snippet, bool HasAttachments);

public sealed record MailDetailDto(
    Guid Id, string AccountKey, string AccountAddress, string Folder,
    string FromName, string FromAddress, string ToAddresses, string Subject,
    DateTime SentAt, string BodyText, string? BodyHtml, bool HasAttachments, string AttachmentNames);

public sealed record MailPageDto(IReadOnlyList<MailListItemDto> Items, int Total);

public sealed record MailAccountDto(
    string Key, string Address, bool Configured, int InboxCount, int SentCount,
    DateTime? LastSyncAt, string? LastError, string Signature = "");
