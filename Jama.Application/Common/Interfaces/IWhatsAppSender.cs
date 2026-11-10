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

public interface IWhatsAppSender
{
    /// <summary>The bill for a closed Non-MOI job. Unlike the quotation notice, the caller needs to KNOW whether
    /// this worked: closing the deal depends on it.</summary>
    Task<WhatsAppSendResult> SendJobBillAsync(
        string toPhoneNumber,
        string customerName,
        string jobNumber,
        decimal amountPayable,
        string billUrl,
        CancellationToken cancellationToken);

    Task SendQuotationSentAsync(
        string toPhoneNumber,
        string customerName,
        string quoteNumber,
        decimal grandTotal,
        Guid quotationId,
        CancellationToken cancellationToken);
}
