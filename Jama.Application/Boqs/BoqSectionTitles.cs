namespace Jama.Application.Boqs;

/// <summary>
/// The section headings a quotation may use, fixed by the business.
///
/// Not free text: a heading is the first thing a client reads down the page, and
/// letting each member of staff invent their own produced "Ground floor" on one
/// quotation and "Car park" on the next for what is the same class of equipment.
/// A caller sending anything else is refused rather than quietly corrected —
/// silently rewriting a heading would put words on a client-facing document that
/// nobody chose.
/// </summary>
public static class BoqSectionTitles
{
    public const string MainCctv = "Main CCTV System";
    public const string CameraAccessories = "Camera Accessories";
    public const string NvrStorage = "NVR & Storage";
    public const string Monitors = "Monitors and Work Stations";
    public const string Switches = "Switch & Components";
    public const string RackUps = "Rack & UPS";
    public const string Kpoi = "Key Point of Interest Camera (KPOI)";
    public const string PassiveComponents = "Passive Components & Cables";
    public const string AccessControl = "Access Control System";

    /// <summary>In the order they should appear on the document.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        MainCctv,
        CameraAccessories,
        NvrStorage,
        Monitors,
        Switches,
        RackUps,
        Kpoi,
        PassiveComponents,
        AccessControl,
    ];

    /// <summary>Case-insensitive: the client sends back what it was given, but a
    /// difference in casing is not a reason to reject a quotation.</summary>
    public static bool IsAllowed(string? title) =>
        !string.IsNullOrWhiteSpace(title)
        && All.Any(x => string.Equals(x, title.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Maps any accepted spelling onto the canonical one, so the stored
    /// heading is the one the business wrote.</summary>
    public static string Canonical(string? title) =>
        All.FirstOrDefault(x => string.Equals(x, title?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? string.Empty;
}
