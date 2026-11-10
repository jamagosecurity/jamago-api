namespace Jama.Domain.Entities;

/// <summary>
/// One service on the Non-MOI price list — "Power adapter replacement, 250 QAR"
/// under Power service. Admin maintains the list the way the stock inventory is
/// maintained for quotations; every maintenance ticket's checklist reads it.
///
/// A ticket line COPIES the name and price when it is written (see
/// MaintenanceRequestLine), so repricing or removing a service here never
/// restates a ticket already raised.
/// </summary>
public class MaintenanceCatalogueItem : BaseEntity
{
    /// <summary>One of the built-in keys (PowerService, Connectivity, Camera,
    /// NvrStorage, Software, Other) or a name admin typed to add a section.</summary>
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional notes in each language, as a stock item carries them.</summary>
    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }

    public decimal Price { get; set; }
    public int SortOrder { get; set; }

    /// <summary>Pictures of the service, the first being its main one.</summary>
    public ICollection<MaintenanceCatalogueImage> Images { get; set; } = [];
}
