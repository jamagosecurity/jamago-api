namespace Jama.Domain.Entities;

/// <summary>
/// A copy of one email that arrived in one of the company's mailboxes, kept so
/// the administrator can read every address from one place. Read-only by design:
/// the mailboxes are only ever read, never changed, and nothing here can be sent.
/// </summary>
public class MailMessage : BaseEntity
{
    /// <summary>Which configured mailbox it came from (the key in the settings).</summary>
    public string AccountKey { get; set; } = string.Empty;

    /// <summary>The company address it was delivered to, e.g. info@jamago.qa.</summary>
    public string AccountAddress { get; set; } = string.Empty;

    /// <summary>"Inbox" (received) or "Sent" (sent by the company).</summary>
    public string Folder { get; set; } = "Inbox";

    /// <summary>IMAP UIDVALIDITY + UID together identify the message in the mailbox;
    /// if the server renumbers the mailbox, the validity changes and we start over
    /// rather than mixing two numberings.</summary>
    public long UidValidity { get; set; }
    public long Uid { get; set; }

    /// <summary>The Message-ID header, kept so a reply can thread under the original.</summary>
    public string? MessageId { get; set; }

    public string FromName { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string ToAddresses { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }

    /// <summary>First couple of hundred characters of the text, for the list.</summary>
    public string Snippet { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;

    /// <summary>The HTML body as received. NEVER rendered directly — the page
    /// shows it in a sandboxed frame with scripts and remote content blocked.</summary>
    public string? BodyHtml { get; set; }

    public bool HasAttachments { get; set; }
    /// <summary>File names only, comma-separated; the files themselves are not stored.</summary>
    public string AttachmentNames { get; set; } = string.Empty;
}

/// <summary>Where the reader got up to in one mailbox, and whether the last check worked.</summary>
public class MailAccountState : BaseEntity
{
    public string AccountKey { get; set; } = string.Empty;
    public string Folder { get; set; } = "Inbox";
    public long UidValidity { get; set; }
    public long LastUid { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string? LastError { get; set; }
}

/// <summary>Who sent what, from which company mailbox, and when. The mailboxes are shared
/// between people, so a send is never anonymous.</summary>
public class MailSendLog : BaseEntity
{
    public string AccountKey { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string ToAddresses { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public Guid SentById { get; set; }
    public string SentByName { get; set; } = string.Empty;
    /// <summary>The message this was a reply to, if any.</summary>
    public Guid? ReplyToMessageId { get; set; }
    public bool CopySavedToSent { get; set; }
}
