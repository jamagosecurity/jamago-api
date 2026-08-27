using Jama.Domain.Enums;

namespace Jama.Application.Cameras;

public sealed record CameraDto(
    Guid Id,
    // Identity
    string ItemName,
    string Brand,
    CameraType Type,
    string ModelNo,
    ProductCategory Category,
    string? SearchKey,
    string? DescriptionEn,
    string? DescriptionAr,
    // Pricing & inventory
    decimal? SupplierCost,
    decimal? Margin,
    decimal? Rate,
    decimal? Discount,
    int Quantity,
    int? LowStock,
    string? HsnCode,
    UnitOfMeasurement Uom,
    decimal? TaxRate,
    ItemType ItemType,
    // Recording profile — what the storage calculator sizes from
    CameraResolution Resolution,
    decimal? BitrateMbps,
    // Warranty
    int? WarrantyValue,
    WarrantyUnit? WarrantyUnit,
    string? Notes,
    IReadOnlyList<CameraImageDto> Images,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    /// <summary>
    /// True when stock has fallen to the item's own threshold. Computed here so
    /// the list, any future dashboard and the row badge all agree on what "low"
    /// means instead of each client re-deriving it.
    /// </summary>
    public bool IsLowStock => LowStock.HasValue && Quantity <= LowStock.Value;
}

public sealed record CameraImageDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    int SortOrder,
    /// <summary>Relative URL the browser can put straight in an img src.</summary>
    string Url);
