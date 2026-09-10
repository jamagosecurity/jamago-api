namespace Jama.Application.Options;

/// <summary>
/// Credentials and template ids for the Twilio WhatsApp integration. Every
/// field defaults to empty, and <see cref="IsConfigured"/> is what callers
/// check before sending — a fresh checkout with no Twilio account yet must
/// boot and save quotations normally, just without notifying anyone.
/// </summary>
public sealed class TwilioSettings
{
    public const string SectionName = "Twilio";

    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>The sender Twilio sends WhatsApp messages from, in the
    /// "whatsapp:+&lt;E.164 number&gt;" form — the sandbox number during
    /// development, a provisioned sender once one is approved.</summary>
    public string WhatsAppFrom { get; set; } = string.Empty;

    /// <summary>
    /// Content SID of the Meta-approved template announcing a sent quotation.
    /// Optional, and empty until Meta approves one: WhatsApp refuses a
    /// business-initiated message without a template, but free text still works
    /// inside a session the customer opened themselves — which is every
    /// conversation in the Sandbox. Empty therefore means "Sandbox testing",
    /// not "misconfigured".
    /// </summary>
    public string QuotationSentContentSid { get; set; } = string.Empty;

    /// <summary>Base URL the quotation PDF link is built from, e.g.
    /// "https://api.jamago.qa". No trailing slash.</summary>
    public string PublicApiBaseUrl { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountSid)
        && !string.IsNullOrWhiteSpace(AuthToken)
        && !string.IsNullOrWhiteSpace(WhatsAppFrom);
}
