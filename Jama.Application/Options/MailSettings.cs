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
