namespace Jama.Application.Common.Interfaces;

/// <summary>
/// Sends WhatsApp notifications through Twilio.
///
/// A failure here is logged, never a reason to fail the write that triggered
/// it — a quotation is saved whether or not the customer's phone ever sees a
/// WhatsApp message about it.
/// </summary>
/// <summary>Whether a WhatsApp message was accepted, and if not, why in a sentence a person can act on.</summary>
public sealed record WhatsAppSendResult(bool Ok, string? Error);

/// <summary>One WhatsApp message: the approved template (<see cref="ContentSid"/> filled with <see cref="Variables"/>)
/// when there is one, otherwise <see cref="Body"/> as plain text.</summary>
public sealed record WhatsAppMessage(string Body, string? ContentSid, IReadOnlyDictionary<string, string> Variables);

public interface IWhatsAppSender
{
    /// <summary>Sends one message to a number in international form ("+97455181866"). Ok means Twilio took it and it
    /// had not failed a few seconds later; Error says why not, in words staff can act on.</summary>
    Task<WhatsAppSendResult> SendAsync(string toPhoneNumber, WhatsAppMessage message, CancellationToken cancellationToken);

    Task SendQuotationSentAsync(
        string toPhoneNumber,
        string customerName,
        string quoteNumber,
        decimal grandTotal,
        Guid quotationId,
        CancellationToken cancellationToken);
}
