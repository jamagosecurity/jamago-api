using System.Globalization;
using System.Text.RegularExpressions;

namespace Jama.Application.TravelClaims;

/// <summary>What could be read from an Uber receipt. Anything not found is null; the person checks and fills the rest.</summary>
public sealed record UberReceiptReading(decimal? Amount, DateOnly? TripOn, TimeOnly? TripTime, string? Pickup, string? Dropoff)
{
    public static readonly UberReceiptReading Nothing = new(null, null, null, null, null);

    /// <summary>Whether the parts a claim needs (amount and day) were both found.</summary>
    public bool Complete => Amount is not null && TripOn is not null;
}

/// <summary>
/// Reads an Uber receipt's text: the total paid, the trip date, the pickup time and the pickup and drop-off places.
/// Uber's layout differs a little between app versions and countries, so this looks for the parts by their shape rather
/// than by position, and never guesses: a part it cannot find is left empty for the person to fill in.
/// </summary>
public static partial class UberReceiptParser
{
    // A money figure like 27.50, 1,234.50 or 27
    private const string Money = @"(\d{1,3}(?:,\d{3})*(?:\.\d{1,2})?|\d+(?:\.\d{1,2})?)";
    private const string Currency = @"(?:QAR|QR|ر\.?\s?ق\.?)";

    [GeneratedRegex(@"^\s*(?:total|amount charged|you paid|amount paid)\b(?<rest>.*)$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex TotalLine();

    [GeneratedRegex(Currency + @"\s*" + Money + "|" + Money + @"\s*" + Currency, RegexOptions.IgnoreCase)]
    private static partial Regex CurrencyAmount();

    [GeneratedRegex(Money)]
    private static partial Regex BareAmount();

    [GeneratedRegex(@"\b(?<m>jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:t(?:ember)?)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)\.?\s+(?<d>\d{1,2})(?:st|nd|rd|th)?,?\s+(?<y>\d{4})\b", RegexOptions.IgnoreCase)]
    private static partial Regex MonthDayYear();

    [GeneratedRegex(@"\b(?<d>\d{1,2})(?:st|nd|rd|th)?\s+(?<m>jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:t(?:ember)?)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)\.?,?\s+(?<y>\d{4})\b", RegexOptions.IgnoreCase)]
    private static partial Regex DayMonthYear();

    [GeneratedRegex(@"\b(?<h>\d{1,2}):(?<min>[0-5]\d)\s*(?<ap>[AaPp]\.?\s?[Mm]\.?)?(?=\W|$)")]
    private static partial Regex TimeOfDay();

    public static UberReceiptReading Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return UberReceiptReading.Nothing;
        text = text.Replace(' ', ' ').Replace("\r", "");
        var lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        var (time, pickup, dropoff) = TimesAndPlaces(lines);
        return new(TotalOf(lines), DateOf(text), time, pickup, dropoff);
    }

    /// <summary>The figure on the "Total" line (not "Subtotal"), or the next line if the figure sits there.</summary>
    private static decimal? TotalOf(List<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var match = TotalLine().Match(lines[i]);
            if (!match.Success) continue;
            var found = AmountIn(match.Groups["rest"].Value) ?? (i + 1 < lines.Count ? AmountIn(lines[i + 1], currencyOnly: true) : null);
            if (found is not null) return found;
        }
        // No "Total" line: the largest figure written with the currency.
        var all = lines.SelectMany(l => CurrencyAmount().Matches(l)).Select(m => ToMoney(m)).Where(x => x is not null).ToList();
        return all.Count == 0 ? null : all.Max();
    }

    private static decimal? AmountIn(string text, bool currencyOnly = false)
    {
        var withCurrency = CurrencyAmount().Match(text);
        if (withCurrency.Success) return ToMoney(withCurrency);
        if (currencyOnly) return null;
        var bare = BareAmount().Match(text);
        return bare.Success && decimal.TryParse(bare.Value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static decimal? ToMoney(Match m)
    {
        var value = m.Groups.Cast<Group>().Skip(1).FirstOrDefault(g => g.Success && g.Value.Length > 0)?.Value;
        return value is not null && decimal.TryParse(value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : null;
    }

    private static DateOnly? DateOf(string text)
    {
        foreach (var regex in new[] { MonthDayYear(), DayMonthYear() })
        {
            var m = regex.Match(text);
            if (!m.Success) continue;
            var month = DateTime.ParseExact(m.Groups["m"].Value[..3], "MMM", CultureInfo.InvariantCulture).Month;
            if (int.TryParse(m.Groups["d"].Value, out var day) && int.TryParse(m.Groups["y"].Value, out var year)
                && day >= 1 && day <= DateTime.DaysInMonth(year, month))
                return new DateOnly(year, month, day);
        }
        return null;
    }

    /// <summary>
    /// The route: a time followed by a place, twice — pickup, then drop-off. Uber's receipt also carries other times (when
    /// the receipt was made, when the payment went through), so: the route is read from below "Trip details" when that
    /// heading is there; a time on the same line as a numeric date (a payment stamp) is skipped; and the line after a time
    /// only counts as a place if it reads like one, not like Uber's own wording.
    /// </summary>
    private static (TimeOnly? Time, string? Pickup, string? Dropoff) TimesAndPlaces(List<string> lines)
    {
        var details = lines.FindIndex(l => TripDetails().IsMatch(l));
        var from = details >= 0 ? details + 1 : 0;
        var stops = new List<(TimeOnly Time, string Place)>();
        TimeOnly? firstTime = null;

        for (var i = from; i < lines.Count && stops.Count < 2; i++)
        {
            var m = TimeOfDay().Match(lines[i]);
            if (!m.Success || !TryTime(m, out var t)) continue;
            if (NumericDate().IsMatch(lines[i])) continue;                 // "11/22/26 1:31 PM": when the payment went through
            firstTime ??= t;
            var rest = lines[i][(m.Index + m.Length)..].Trim(' ', '|', '-', '·', '•');
            var place = LooksLikePlace(rest) ? rest
                : i + 1 < lines.Count && LooksLikePlace(lines[i + 1]) ? lines[i + 1]
                : null;
            if (place is not null) stops.Add((t, Clip(place)!));
        }

        // No route found below the heading (or no heading at all): fall back to the first time anywhere, so the trip
        // still gets a time; the places stay empty for the person to fill in.
        if (stops.Count == 0 && firstTime is null)
            firstTime = lines.Select(l => TimeOfDay().Match(l)).Where(m => m.Success && !NumericDate().IsMatch(m.Value))
                .Select(m => TryTime(m, out var t) ? t : (TimeOnly?)null).FirstOrDefault(t => t is not null);

        return stops.Count switch
        {
            0 => (firstTime, null, null),
            1 => (stops[0].Time, stops[0].Place, null),
            _ => (stops[0].Time, stops[0].Place, stops[1].Place),
        };
    }

    /// <summary>A place reads like an address: some letters, not a time or a total, and not one of Uber's own sentences.</summary>
    private static bool LooksLikePlace(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim();
        return t.Length is >= 4 and <= 160
            && t.Any(char.IsLetter)
            && !TimeOfDay().IsMatch(t)
            && !TotalLine().IsMatch(t)
            && !CurrencyAmount().IsMatch(t)
            && !UberWording().IsMatch(t);
    }

    [GeneratedRegex(@"^\s*trip details\b", RegexOptions.IgnoreCase)]
    private static partial Regex TripDetails();

    [GeneratedRegex(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b")]
    private static partial Regex NumericDate();

    [GeneratedRegex(@"^(thanks for riding|we hope|here'?s your receipt|visit the trip page|this is not|fare does not|payments?\b|trip fare|subtotal|booking fee|you rode with|license plate|want to review|uber\s*one|uberx|uberxl|comfort|green|kilomet|miles?\b|\d+(\.\d+)?\s*(kilomet|km|mi))", RegexOptions.IgnoreCase)]
    private static partial Regex UberWording();

    private static bool TryTime(Match m, out TimeOnly time)
    {
        time = default;
        if (!int.TryParse(m.Groups["h"].Value, out var h) || !int.TryParse(m.Groups["min"].Value, out var min)) return false;
        var ap = m.Groups["ap"].Value.Replace(".", "").Replace(" ", "").ToUpperInvariant();
        if (ap == "PM" && h < 12) h += 12;
        if (ap == "AM" && h == 12) h = 0;
        if (h > 23) return false;
        time = new TimeOnly(h, min);
        return true;
    }

    private static string? Clip(string? place) =>
        string.IsNullOrWhiteSpace(place) ? null : place.Length > 300 ? place[..300] : place;
}
