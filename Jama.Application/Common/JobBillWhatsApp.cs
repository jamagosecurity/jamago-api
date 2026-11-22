using System.Globalization;
using Jama.Application.Common.Interfaces;
using Jama.Application.Options;

namespace Jama.Application.Common;

/// <summary>
/// The WhatsApp messages of a job bill (MOI and Non-MOI alike), the same steps as the emails: the bill with the link to
/// the client's page (Approve / Reject / Comment), then a thank-you with the payment details when they approve, or a
/// receipt when they reject or comment.
///
/// Each carries both forms. WhatsApp only delivers a message the business starts with a template Meta has approved, so
/// once a template's Content SID is in the settings that is what goes out, filled with the variables below. Until then
/// the plain text is sent — it arrives only inside a chat the client opened themselves, which is how the Sandbox works.
/// Template variables may not contain line breaks, so the payment details are written into the approved template itself.
/// </summary>
public static class JobBillWhatsApp
{
    private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

    /// <summary>Template: "Hello {{1}}, your bill {{2}} from Jama Go Security Equipment is ready. Amount payable: QAR {{3}}.
    /// Please open it to approve, reject or comment: {{4}}"</summary>
    public static WhatsAppMessage Bill(TwilioSettings settings, string customerName, string jobNumber, decimal amountPayable, string billUrl) =>
        new(
            $"Hello {customerName}, your bill {jobNumber} from Jama Go Security Equipment is ready.\n"
                + $"Amount payable: QAR {Money(amountPayable)}\n\n"
                + $"Please open it to approve, reject or comment:\n{billUrl}\n\n"
                + $"Payment details:\n{CompanyPaymentDetails.AsText()}\n\n"
                + "Jama Go Security Equipment",
            Template(settings.JobBillContentSid),
            new Dictionary<string, string> { ["1"] = customerName, ["2"] = jobNumber, ["3"] = Money(amountPayable), ["4"] = billUrl });

    /// <summary>Template: "Dear {{1}}, thank you for approving invoice {{2}} for QAR {{3}}. You can pay using these
    /// details: …(the payment details, written into the template)… Jama Go Security Equipment"</summary>
    public static WhatsAppMessage Approved(TwilioSettings settings, string customerName, string jobNumber, decimal amountPayable) =>
        new(
            $"Dear {customerName}, thank you for approving invoice {jobNumber} for QAR {Money(amountPayable)}.\n\n"
                + $"You can pay using these details:\n{CompanyPaymentDetails.AsText()}\n\n"
                + "Jama Go Security Equipment",
            Template(settings.JobApprovedContentSid),
            new Dictionary<string, string> { ["1"] = customerName, ["2"] = jobNumber, ["3"] = Money(amountPayable) });

    /// <summary>Template: "Dear {{1}}, we have received your {{2}} on invoice {{3}}. We will look at it and send you an
    /// updated invoice soon. Jama Go Security Equipment"</summary>
    public static WhatsAppMessage ReplyReceived(TwilioSettings settings, string customerName, string jobNumber, bool rejected)
    {
        var what = rejected ? "rejection" : "comment";
        return new(
            $"Dear {customerName}, we have received your {what} on invoice {jobNumber}.\n"
                + "We will look at it and send you an updated invoice soon.\n\n"
                + "Jama Go Security Equipment",
            Template(settings.JobReplyReceivedContentSid),
            new Dictionary<string, string> { ["1"] = customerName, ["2"] = what, ["3"] = jobNumber });
    }

    private static string? Template(string? sid) => string.IsNullOrWhiteSpace(sid) ? null : sid.Trim();
}
