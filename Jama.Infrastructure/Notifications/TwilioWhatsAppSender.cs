using Jama.Application.Common.Interfaces;
using Jama.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Jama.Infrastructure.Notifications;

/// <summary>
/// Announces a sent quotation over WhatsApp via Twilio's Content Template API.
/// A template is required rather than a free-text body because the message is
/// always business-initiated — see <see cref="TwilioSettings.QuotationSentContentSid"/>.
/// </summary>
public sealed class TwilioWhatsAppSender : IWhatsAppSender
{
    private readonly TwilioSettings _settings;
    private readonly ILogger<TwilioWhatsAppSender> _logger;

    // TwilioClient.Init sets a process-wide static client; guarded so the many
    // short-lived instances DI creates per request only pay for it once.
    private static bool _clientInitialised;
    private static readonly object InitLock = new();

    public TwilioWhatsAppSender(IOptions<TwilioSettings> settings, ILogger<TwilioWhatsAppSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendQuotationSentAsync(
        string toPhoneNumber,
        string customerName,
        string quoteNumber,
        decimal grandTotal,
        Guid quotationId,
        CancellationToken cancellationToken)
    {
        if (!_settings.IsConfigured)
        {
            _logger.LogWarning(
                "Twilio is not configured; skipped WhatsApp notification for quote {QuoteNumber}.",
                quoteNumber);
            return;
        }

        EnsureClientInitialised();

        try
        {
            var pdfUrl = $"{_settings.PublicApiBaseUrl.TrimEnd('/')}/api/quotations/{quotationId}/pdf";
            var to = new PhoneNumber($"whatsapp:{NormalizeE164(toPhoneNumber)}");
            var from = new PhoneNumber(_settings.WhatsAppFrom);

            if (string.IsNullOrWhiteSpace(_settings.QuotationSentContentSid))
            {
                // No approved template yet. WhatsApp only accepts free text inside
                // a session the customer opened themselves — which is exactly the
                // case in the Sandbox, where the recipient joins by messaging in.
                // Production sets the template SID and takes the branch below.
                await MessageResource.CreateAsync(
                    to: to,
                    from: from,
                    body: $"Hello {customerName}, your quotation {quoteNumber} from Jama Go Security "
                        + $"Equipment is ready. Total: QAR {grandTotal:N2}. You can view it here: {pdfUrl}");
                return;
            }

            var contentVariables = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["1"] = customerName,
                ["2"] = quoteNumber,
                ["3"] = grandTotal.ToString("N2"),
                ["4"] = pdfUrl,
            });

            await MessageResource.CreateAsync(
                to: to,
                from: from,
                contentSid: _settings.QuotationSentContentSid,
                contentVariables: contentVariables);
        }
        catch (Exception ex)
        {
            // Notification delivery is best-effort: the quotation itself is
            // already saved by the time this runs, and a Twilio outage or a bad
            // phone number must not surface as a failed save.
            _logger.LogError(
                ex,
                "Failed to send WhatsApp notification for quote {QuoteNumber} to {Phone}.",
                quoteNumber,
                toPhoneNumber);
        }
    }

    private void EnsureClientInitialised()
    {
        if (_clientInitialised)
            return;

        lock (InitLock)
        {
            if (_clientInitialised)
                return;

            TwilioClient.Init(_settings.AccountSid, _settings.AuthToken);
            _clientInitialised = true;
        }
    }

    /// <summary>Twilio requires E.164 (a leading '+', no spaces). Customer
    /// phone numbers are free text on the quotation form, so this is a best
    /// effort rather than a validator — a number that still doesn't fit fails
    /// the Twilio call, which is caught and logged above.</summary>
    private static string NormalizeE164(string phone)
    {
        var digits = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
        return digits.StartsWith('+') ? digits : $"+{digits}";
    }
}
