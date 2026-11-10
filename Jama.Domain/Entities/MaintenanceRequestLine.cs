using Jama.Domain.Enums;

namespace Jama.Domain.Entities;

/// <summary>
/// One issue on a maintenance ticket. A ticket is a site visit, and a visit
/// usually fixes more than one thing, so a ticket holds lines the way a
/// quotation does — grouped by category, each with its own quantity and fee.
///
/// Priced on the same terms as a BoqLine: the fee is copied in when the line
/// is written, and the catalogue's own figure is kept beside it so a
/// negotiated price stays visible as a variance rather than silently replacing
/// the list price.
/// </summary>
public class MaintenanceRequestLine : BaseEntity
{
    public Guid MaintenanceRequestId { get; set; }
    public MaintenanceRequest MaintenanceRequest { get; set; } = null!;

    public string Category { get; set; } = string.Empty;

    /// <summary>Copied from the Non-MOI price list (MaintenanceCatalogueItem)
    /// when the line is written — or typed, for an issue the list does not
    /// name, rather than blocking it.</summary>
    public string IssueType { get; set; } = string.Empty;

    /// <summary>Copied from the price list with the fee, so the ticket keeps
    /// describing what was done after the service is reworded.</summary>
    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }

    public int Quantity { get; set; } = 1;

    /// <summary>What this line is charged at. Defaults to <see cref="CatalogueFee"/>;
    /// only maintenance.manage may set a different one.</summary>
    public decimal UnitFee { get; set; }

    /// <summary>The catalogue's price for this issue type when the line was
    /// written, whether or not anyone overrode it.</summary>
    public decimal CatalogueFee { get; set; }

    public decimal LineTotal { get; set; }
    public int SortOrder { get; set; }
}
