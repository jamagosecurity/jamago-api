namespace Jama.Application.Common;

/// <summary>
/// Turns a customer number as staff type it into the international form WhatsApp needs: "5518 1866",
/// "+974 5518 1866" and "00974 5518 1866" all become "+97455181866". Numbers here are typed for Qatar, so eight
/// digits with no country code get +974. Anything that cannot be a phone number gives null.
/// </summary>
public static class WhatsAppNumbers
{
    public static string? ToE164(string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed)) return null;
        var text = typed.Trim();
        var international = text.StartsWith('+');
        var digits = new string(text.Where(char.IsDigit).ToArray());

        if (!international && digits.StartsWith("00"))
        {
            digits = digits[2..];
            international = true;
        }
        if (!international && digits.Length == 8)
            digits = "974" + digits;          // a Qatar number typed without the country code

        return digits.Length is >= 8 and <= 15 ? "+" + digits : null;
    }
}
