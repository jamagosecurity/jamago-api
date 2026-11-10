using Jama.Domain.Entities;

namespace Jama.Application.Maintenance;

/// <summary>
/// Prices an issue from the Non-MOI price list admin maintains (the
/// MaintenanceCatalogueItems table) — the same list the checklist shows, so
/// what a client or technician sees ticked is what the ticket charges.
/// </summary>
public static class MaintenanceIssueCatalogue
{
    /// <summary>The charge for an issue the list does not name — a typed
    /// "Other" issue, or a service admin has since removed.</summary>
    public const decimal StandardFee = 250m;

    /// <summary>The listed service of this name in this category; failing
    /// that, one of that name in any category; null when the list does not
    /// name it. Case-insensitive.</summary>
    public static MaintenanceCatalogueItem? Find(
        string category, string issueType, IReadOnlyCollection<MaintenanceCatalogueItem> catalogue)
    {
        var name = issueType.Trim();
        var sameName = catalogue.Where(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        return sameName.FirstOrDefault(i => i.Category == category) ?? sameName.FirstOrDefault();
    }

    /// <summary>The listed price for this issue — see <see cref="Find"/> —
    /// or <see cref="StandardFee"/> when the list does not name it.</summary>
    public static decimal FeeFor(
        string category, string issueType, IReadOnlyCollection<MaintenanceCatalogueItem> catalogue) =>
        Find(category, issueType, catalogue)?.Price ?? StandardFee;
}
