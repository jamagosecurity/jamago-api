namespace Jama.Application.Common.Interfaces;

/// <summary>
/// Sends WhatsApp notifications through Twilio.
///
/// A failure here is logged, never a reason to fail the write that triggered
/// it — a quotation is saved whether or not the customer's phone ever sees a
/// WhatsApp message about it.
/// </summary>
public interface IWhatsAppSender
{
    Task SendQuotationSentAsync(
        string toPhoneNumber,
        string customerName,
        string quoteNumber,
        decimal grandTotal,
        Guid quotationId,
        CancellationToken cancellationToken);
}
