namespace Jama.Application.MoiJobs.Services;

/// <summary>
/// Categories on the MOI price list. Unlike the Non-MOI list there are no built-in ones: the categories are whatever
/// the team creates. "Other" always sorts last.
/// </summary>
public static class MoiServiceCategories
{
    public const int MaxLength = 60;
    public const string Other = "Other";

    /// <summary>Tidies spacing and reuses the existing spelling of a category already on the list.</summary>
    public static string Normalize(string? raw, IEnumerable<string>? known = null)
    {
        var text = string.Join(' ', (raw ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (text.Length == 0) return string.Empty;
        return known?.FirstOrDefault(k => string.Equals(k, text, StringComparison.OrdinalIgnoreCase)) ?? text;
    }

    /// <summary>MOI categories are stored as they are shown.</summary>
    public static string Label(string category) => category;

    /// <summary>No built-in categories on the MOI list — every one can be renamed or deleted.</summary>
    public static bool IsBuiltIn(string category) => false;

    public static int Rank(string category) =>
        string.Equals(category, Other, StringComparison.OrdinalIgnoreCase) ? int.MaxValue : 0;
}
