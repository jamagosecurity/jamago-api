namespace Jama.Application.Maintenance;

/// <summary>
/// The sections a maintenance checklist is split into. Six are built in and
/// keep fixed keys (so tickets and prices written before categories could be
/// created still resolve); admin can add more from the Non-MOI price list,
/// and those are stored as the typed name itself.
/// </summary>
public static class MaintenanceCategories
{
    public const int MaxLength = 60;

    public const string Other = "Other";

    private static readonly (string Key, string Label)[] BuiltIn =
    [
        ("PowerService", "Power service"),
        ("Connectivity", "Connectivity"),
        ("Camera", "Camera"),
        ("NvrStorage", "NVR & storage"),
        ("Software", "Software"),
        (Other, "Other"),
    ];

    /// <summary>The stored form of a category: whitespace tidied, a built-in
    /// typed by its key or its label folded to the key, and a custom name
    /// folded to the casing already in <paramref name="known"/> so "access
    /// control" and "Access Control" cannot become two sections. Empty when
    /// nothing was given.</summary>
    public static string Normalize(string? raw, IEnumerable<string>? known = null)
    {
        var text = string.Join(' ', (raw ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (text.Length == 0)
            return string.Empty;

        foreach (var (key, label) in BuiltIn)
        {
            if (string.Equals(text, key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, label, StringComparison.OrdinalIgnoreCase))
                return key;
        }

        return known?.FirstOrDefault(k => string.Equals(k, text, StringComparison.OrdinalIgnoreCase)) ?? text;
    }

    /// <summary>The six built-ins keep their keys and cannot be renamed or
    /// removed — tickets and prices are written against them.</summary>
    /// <summary>The words to print for a stored category: "NvrStorage" → "NVR &amp; storage"; a custom name as typed.</summary>
    public static string Label(string category)
    {
        var found = Array.Find(BuiltIn, b => b.Key == category);
        return found.Label ?? category;
    }

    public static bool IsBuiltIn(string category) => Array.Exists(BuiltIn, b => b.Key == category);

    /// <summary>Checklist order: the built-ins as listed, then added
    /// categories, with Other always last.</summary>
    public static int Rank(string category)
    {
        var index = Array.FindIndex(BuiltIn, b => b.Key == category);
        if (index < 0)
            return BuiltIn.Length - 1;
        return category == Other ? int.MaxValue : index;
    }
}
