namespace Jama.Domain.Entities;

/// <summary>
/// One priced row on a quotation.
///
/// The item's name, model, brand and description are COPIED here rather than
/// read through <see cref="CameraId"/>. A quotation is a historical document: if
/// the stock item is later renamed, repriced or deleted, a quote already sent to
/// a customer must still say what it said at the time. CameraId is kept only as
/// a soft link back to the catalogue, and is null once that item is gone.
/// </summary>
public class QuotationLine : BaseEntity
{
    public Guid QuotationId { get; set; }
    public Quotation Quotation { get; set; } = null!;

    public Guid? CameraId { get; set; }
    public Camera? Camera { get; set; }

    public string ItemName { get; set; } = string.Empty;
    public string? ModelNo { get; set; }
    public string? Brand { get; set; }
    public string? Description { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitRate { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal TaxPercent { get; set; }

    /// <summary>Net of discount and inclusive of tax. Computed by QuotationMath.</summary>
    public decimal LineTotal { get; set; }

    public int SortOrder { get; set; }
}
