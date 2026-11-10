namespace Jama.Application.Common;

/// <summary>
/// Where a client pays. Printed on the bill, written in the email and in the WhatsApp message, so the three
/// can never disagree. Change it here and everything that is sent from then on carries the new details.
/// </summary>
public static class CompanyPaymentDetails
{
    public const string AccountName = "FAWRAN - JAMA GO SECURITY EQUIPMENT";
    public const string CrNumber = "189177";
    public const string Iban = "QA11QIIB 0000 0000 1113196013001";

    /// <summary>Three lines, for an email or a message.</summary>
    public static string AsText() => $"{AccountName}\nCR NO: {CrNumber}\nIBAN: {Iban}";
}
