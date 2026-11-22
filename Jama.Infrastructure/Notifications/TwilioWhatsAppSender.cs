using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Jama.Infrastructure.Notifications;

/// <summary>
/// Sends WhatsApp messages through Twilio: job bills and their follow-ups (see <c>JobBillWhatsApp</c>), and the quotation
/// announcement. A business-initiated message needs a Meta-approved template; without one, plain text is sent, which
/// only arrives inside a chat the customer opened (the Sandbox).
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
            var to = new PhoneNumber($"whatsapp:{WhatsAppNumbers.ToE164(toPhoneNumber) ?? toPhoneNumber}");
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

    public async Task<WhatsAppSendResult> SendAsync(string toPhoneNumber, WhatsAppMessage message, CancellationToken cancellationToken)
    {
        if (!_settings.IsConfigured)
            return new WhatsAppSendResult(false, "WhatsApp is not set up on this server yet.");

        var number = WhatsAppNumbers.ToE164(toPhoneNumber);
        if (number is null)
            return new WhatsAppSendResult(false, $"“{toPhoneNumber}” is not a phone number WhatsApp can use.");

        EnsureClientInitialised();

        try
        {
            var to = new PhoneNumber($"whatsapp:{number}");
            var from = new PhoneNumber(_settings.WhatsAppFrom);
            var sent = message.ContentSid is null
                ? await MessageResource.CreateAsync(to: to, from: from, body: message.Body)
                : await MessageResource.CreateAsync(to: to, from: from, contentSid: message.ContentSid,
                    contentVariables: System.Text.Json.JsonSerializer.Serialize(message.Variables));

            // Twilio accepts a message first and finds out a moment later whether WhatsApp delivers it (not on WhatsApp,
            // no approved template…). A short look afterwards turns the common failures into an answer staff can act on now.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(1.5), cancellationToken);
                var now = await MessageResource.FetchAsync(pathSid: sent.Sid);
                if (now.Status == MessageResource.StatusEnum.Failed || now.Status == MessageResource.StatusEnum.Undelivered)
                {
                    _logger.LogWarning("WhatsApp message {Sid} to {Phone} failed with Twilio error {Code}.", sent.Sid, number, now.ErrorCode);
                    return new WhatsAppSendResult(false, Explain(now.ErrorCode, now.ErrorMessage));
                }
                if (now.Status == MessageResource.StatusEnum.Delivered || now.Status == MessageResource.StatusEnum.Read)
                    break;
            }

            return new WhatsAppSendResult(true, null);
        }
        catch (Twilio.Exceptions.ApiException ex)
        {
            _logger.LogError(ex, "Twilio refused a WhatsApp message to {Phone} (error {Code}).", number, ex.Code);
            return new WhatsAppSendResult(false, Explain(ex.Code, ex.Message));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Could not send a WhatsApp message to {Phone}.", number);
            return new WhatsAppSendResult(false, "WhatsApp could not send the message right now. Please try again.");
        }
    }

    /// <summary>Twilio's error codes in words staff can act on.</summary>
    private static string Explain(int? code, string? twilioMessage) => code switch
    {
        63003 or 21211 or 21614 => "This number does not seem to be on WhatsApp. Check the customer number, or send the bill by email.",
        63015 => "This WhatsApp test number can only message phones that joined the Twilio Sandbox first.",
        63016 => "WhatsApp needs an approved message template to start a chat with this client. Send by email for now.",
        63007 or 63018 or 21606 => "The company WhatsApp sender is not ready on Twilio yet.",
        20003 => "The Twilio login on this server is wrong.",
        _ => "WhatsApp could not deliver the message" + (string.IsNullOrWhiteSpace(twilioMessage) ? "." : $": {twilioMessage}"),
    };

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
}
