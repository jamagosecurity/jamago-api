using System.Globalization;

namespace Jama.Infrastructure.Documents;

/// <summary>
/// An amount of Qatari riyals written out as a sentence, in English and in Arabic.
///
/// Every priced document carries this, and not for decoration. A figure can be
/// altered after it leaves here with one keystroke and no trace; the same edit to
/// a sentence has to be rewritten to agree with it, and a total that disagrees
/// with its own words is a document nobody has to honour. It is also what a bank
/// reads first on a cheque drawn against the quotation.
///
/// Both languages close the sentence — "only", "لا غير" — for the same reason:
/// the phrase marks the end of the amount, so nothing can be appended to it.
/// </summary>
internal static class MoneyWords
{
    /// <summary>
    /// "Eighty-five thousand two hundred forty-eight riyals and seventy dirhams only".
    /// </summary>
    internal static string English(decimal amount)
    {
        var (riyals, dirhams) = Split(amount);

        var text = $"{SpellEnglish(riyals)} {(riyals == 1 ? "riyal" : "riyals")}";

        // A whole amount says nothing about dirhams rather than "and zero
        // dirhams", which reads like a field nobody filled in.
        if (dirhams > 0)
            text += $" and {SpellEnglish(dirhams)} {(dirhams == 1 ? "dirham" : "dirhams")}";

        return $"{char.ToUpperInvariant(text[0])}{text[1..]} only";
    }

    /// <summary>
    /// "فقط خمسة وثمانون ألفاً ومائتان وثمانية وأربعون ريالاً قطرياً وسبعون درهماً لا غير".
    /// </summary>
    internal static string Arabic(decimal amount)
    {
        var (riyals, dirhams) = Split(amount);

        var text = $"فقط {CountedArabic(riyals, RiyalWords)}";

        if (dirhams > 0)
            text += $" و{CountedArabic(dirhams, DirhamWords)}";

        return $"{text} لا غير";
    }

    /// <summary>
    /// Splits an amount into whole riyals and fils.
    ///
    /// Rounded away from zero to match the printed figure, which is rounded the
    /// same way. Half a dirham resolving in opposite directions would put the
    /// words and the number one dirham apart — the exact disagreement this class
    /// exists to make impossible.
    /// </summary>
    private static (long Riyals, int Dirhams) Split(decimal amount)
    {
        var rounded = Math.Round(Math.Abs(amount), 2, MidpointRounding.AwayFromZero);
        var riyals = (long)decimal.Truncate(rounded);

        return (riyals, (int)decimal.Truncate((rounded - riyals) * 100m));
    }

    // ===== English =====

    private static readonly string[] EnglishToNineteen =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
        "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen",
        "eighteen", "nineteen",
    ];

    private static readonly string[] EnglishTens =
        ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    private static readonly (long Value, string Name)[] EnglishScales =
        [(1_000_000_000L, "billion"), (1_000_000L, "million"), (1_000L, "thousand")];

    /// <summary>Spells a whole number: 248 becomes "two hundred forty-eight".</summary>
    private static string SpellEnglish(long value)
    {
        if (value < 20) return EnglishToNineteen[value];

        if (value < 100)
        {
            var tens = EnglishTens[value / 10];
            return value % 10 == 0 ? tens : $"{tens}-{EnglishToNineteen[value % 10]}";
        }

        if (value < 1_000)
        {
            var hundreds = $"{EnglishToNineteen[value / 100]} hundred";
            return value % 100 == 0 ? hundreds : $"{hundreds} {SpellEnglish(value % 100)}";
        }

        foreach (var (scale, name) in EnglishScales)
        {
            if (value < scale) continue;

            var head = $"{SpellEnglish(value / scale)} {name}";
            return value % scale == 0 ? head : $"{head} {SpellEnglish(value % scale)}";
        }

        return value.ToString(CultureInfo.InvariantCulture);
    }

    // ===== Arabic =====

    private static readonly string[] ArabicToNineteen =
    [
        "صفر", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة", "ستة", "سبعة", "ثمانية", "تسعة",
        "عشرة", "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر", "ستة عشر",
        "سبعة عشر", "ثمانية عشر", "تسعة عشر",
    ];

    private static readonly string[] ArabicTens =
        ["عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون"];

    private static readonly string[] ArabicHundreds =
    [
        "مائة", "مائتان", "ثلاثمائة", "أربعمائة", "خمسمائة", "ستمائة", "سبعمائة",
        "ثمانمائة", "تسعمائة",
    ];

    /// <summary>
    /// The scale words, each in the four forms Arabic counts with: one, two,
    /// three-to-ten, and everything above — "ألف، ألفان، آلاف، ألفاً".
    /// </summary>
    private static readonly (long Value, string One, string Two, string Few, string Many)[] ArabicScales =
    [
        (1_000_000_000L, "مليار", "ملياران", "مليارات", "ملياراً"),
        (1_000_000L, "مليون", "مليونان", "ملايين", "مليوناً"),
        (1_000L, "ألف", "ألفان", "آلاف", "ألفاً"),
    ];

    private static readonly (string One, string Two, string Few, string Many) RiyalWords =
        ("ريال قطري", "ريالان قطريان", "ريالات قطرية", "ريالاً قطرياً");

    private static readonly (string One, string Two, string Few, string Many) DirhamWords =
        ("درهم", "درهمان", "دراهم", "درهماً");

    /// <summary>
    /// A number and the thing it counts, agreeing the way Arabic does.
    ///
    /// The form of the counted noun follows the last two digits, not the whole
    /// number: 1,001 riyals ends "ريال قطري" exactly as 1 does, and 25 ends
    /// "ريالاً قطرياً" exactly as 125 does. One and two carry the count inside the
    /// noun itself — "ريالان قطريان" already says two — so the numeral is dropped.
    /// </summary>
    private static string CountedArabic(
        long value, (string One, string Two, string Few, string Many) noun)
    {
        if (value == 1) return $"{noun.One} واحد";
        if (value == 2) return noun.Two;

        var lastTwo = value % 100;
        var word = lastTwo switch
        {
            // A round hundred or thousand takes the singular: "مائة ريال قطري".
            0 or 1 => noun.One,
            2 => noun.Two,
            >= 3 and <= 10 => noun.Few,
            _ => noun.Many,
        };

        return $"{SpellArabic(value)} {word}";
    }

    /// <summary>Spells a whole number: 248 becomes "مائتان وثمانية وأربعون".</summary>
    private static string SpellArabic(long value)
    {
        if (value < 1_000) return SpellArabicGroup(value);

        var parts = new List<string>();
        var remainder = value;

        foreach (var (scale, one, two, few, many) in ArabicScales)
        {
            var count = remainder / scale;
            if (count == 0) continue;

            remainder %= scale;

            parts.Add(count switch
            {
                1 => one,
                2 => two,
                >= 3 and <= 10 => $"{SpellArabicGroup(count)} {few}",
                _ => $"{SpellArabic(count)} {many}",
            });
        }

        if (remainder > 0) parts.Add(SpellArabicGroup(remainder));

        return string.Join(" و", parts);
    }

    /// <summary>Nought to 999. Units come before tens — "خمسة وعشرون" is
    /// five-and-twenty — which is why this cannot fall out of the English shape.</summary>
    private static string SpellArabicGroup(long value)
    {
        if (value == 0) return ArabicToNineteen[0];

        var parts = new List<string>();

        if (value >= 100)
        {
            parts.Add(ArabicHundreds[value / 100 - 1]);
            value %= 100;
        }

        if (value > 0)
        {
            if (value < 20)
            {
                parts.Add(ArabicToNineteen[value]);
            }
            else
            {
                var units = value % 10;
                var tens = ArabicTens[value / 10 - 2];
                parts.Add(units == 0 ? tens : $"{ArabicToNineteen[units]} و{tens}");
            }
        }

        return string.Join(" و", parts);
    }
}
