using Jama.Domain.Enums;

namespace Jama.Domain.Entities;

/// <summary>
/// One change to the Non-MOI inventory — an item added, its price changed, or its details edited — with who did it
/// and when. Technicians as well as the office can add items and change prices, so every change is kept.
///
/// No foreign key on purpose: the log outlives an item that is later deleted, so it keeps the name as it was.
/// </summary>
public class MaintenanceCatalogueChange : BaseEntity
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public CatalogueChangeAction Action { get; set; }
    public decimal? OldPrice { get; set; }
    public decimal? NewPrice { get; set; }
    public Guid ById { get; set; }
    public string ByName { get; set; } = string.Empty;
}
