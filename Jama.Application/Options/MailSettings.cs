namespace Jama.Application.Options;

/// <summary>
/// The company mailboxes the admin panel reads. Passwords live ONLY in the private
/// settings file or environment variables (never in the code or the repository).
/// An account with an empty password is skipped, so a fresh checkout boots fine.
/// </summary>
public sealed class MailSettings
{
    public const string SectionName = "Mail";

    /// <summary>How often each mailbox is checked, in minutes.</summary>
    public int PollMinutes { get; set; } = 5;

    /// <summary>How many of the newest messages to copy the first time a mailbox is read.</summary>
    public int FirstSyncMessages { get; set; } = 300;

    /// <summary>The public address of the website, used to build the links in a client's bill email and WhatsApp message
    /// (the page where they approve, reject or comment). No trailing slash.</summary>
    public string PublicSiteUrl { get; set; } = "https://jamago.qa";

    /// <summary>The copy mailbox (e.g. "accounts"): every mailbox forwards a copy of its incoming mail here (set by the
    /// owner in hPanel), so new mail keeps showing even when someone changes their own password. Mail read from it is
    /// filed under the mailbox it was sent to.</summary>
    public string ArchiveKey { get; set; } = string.Empty;

    /// <summary>The company's main inbox: copied on every client approval of a Non-MOI bill.</summary>
    public string CompanyInbox { get; set; } = "info@jamago.qa";

    public List<MailAccountSettings> Accounts { get; set; } = [];
}

public sealed class MailAccountSettings
{
    /// <summary>Short stable id, e.g. "info". Used to filter in the panel.</summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>The address, e.g. info@jamago.qa. Shown in the panel.</summary>
    public string Address { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 993;
    public bool UseSsl { get; set; } = true;
    /// <summary>Usually the full address. Defaults to <see cref="Address"/> when empty.</summary>
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>Appended to new messages, replies and forwards from this mailbox. Plain text; the sender can edit it before sending.</summary>
    public string Signature { get; set; } = string.Empty;

    /// <summary>The person behind the mailbox, for the signature card: e.g. "Yousef Kotb", "Pre-Sales", "+974 3139 9511".
    /// Left empty on a shared mailbox (info, support…) the card shows the department instead.</summary>
    public string SignatureName { get; set; } = string.Empty;
    public string SignatureTitle { get; set; } = string.Empty;
    public string SignatureMobile { get; set; } = string.Empty;

    /// <summary>Outgoing server. Left empty it is derived from <see cref="Host"/> (imap.x.com → smtp.x.com).</summary>
    public string SmtpHost { get; set; } = string.Empty;
    /// <summary>465 = TLS from the first byte; anything else (587) starts plain and upgrades with STARTTLS.</summary>
    public int SmtpPort { get; set; } = 465;

    public string ResolvedSmtpHost =>
        !string.IsNullOrWhiteSpace(SmtpHost) ? SmtpHost
        : Host.StartsWith("imap.", StringComparison.OrdinalIgnoreCase) ? "smtp." + Host[5..]
        : Host;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Password);
    public string Login => string.IsNullOrWhiteSpace(Username) ? Address : Username;
}
