using System.Linq.Expressions;
using Jama.Domain.Entities;

namespace Jama.Application.Cameras;

internal static class CameraMappings
{
    /// <summary>Where the API serves an image's bytes from.</summary>
    internal static string ImageUrl(Guid imageId) => $"/api/cameras/images/{imageId}";

    /// <summary>
    /// Projection used by the list query so EF selects only these columns rather
    /// than materialising entities it would immediately discard. The image
    /// sub-select is part of the same expression, so the whole page still costs
    /// one round trip rather than one per row.
    /// </summary>
    internal static readonly Expression<Func<Camera, CameraDto>> Projection =
        entity => new CameraDto(
            entity.Id,
            entity.ItemName,
            entity.Brand,
            entity.Type,
            entity.ModelNo,
            entity.Category,
            entity.SearchKey,
            entity.DescriptionEn,
            entity.DescriptionAr,
            entity.SupplierCost,
            entity.Margin,
            entity.Rate,
            entity.Discount,
            entity.Quantity,
            entity.LowStock,
            entity.HsnCode,
            entity.Uom,
            entity.TaxRate,
            entity.ItemType,
            entity.Resolution,
            entity.BitrateMbps,
            entity.WarrantyValue,
            entity.WarrantyUnit,
            entity.Notes,
            entity.Images
                .OrderBy(image => image.SortOrder)
                .Select(image => new CameraImageDto(
                    image.Id,
                    image.FileName,
                    image.ContentType,
                    image.SizeBytes,
                    image.SortOrder,
                    "/api/cameras/images/" + image.Id))
                .ToList(),
            entity.CreatedAt,
            entity.UpdatedAt);

    /// <summary>
    /// Blanks what the business paid, for a reader not entitled to it.
    ///
    /// The catalogue is public — the marketing site prices from it — and the
    /// same DTO serves that and the stock screens. Supplier cost and margin are
    /// the two fields on it that are nobody's business but ours, so they are
    /// removed on the way out rather than the response being trusted to reach
    /// only the right eyes.
    ///
    /// Stripped here, in the one place both the list and the detail read pass
    /// through, so a new endpoint cannot forget.
    /// </summary>
    internal static CameraDto WithoutCost(CameraDto dto) =>
        dto with { SupplierCost = null, Margin = null };

    internal static CameraDto ToDto(Camera entity) =>
        new(
            entity.Id,
            entity.ItemName,
            entity.Brand,
            entity.Type,
            entity.ModelNo,
            entity.Category,
            entity.SearchKey,
            entity.DescriptionEn,
            entity.DescriptionAr,
            entity.SupplierCost,
            entity.Margin,
            entity.Rate,
            entity.Discount,
            entity.Quantity,
            entity.LowStock,
            entity.HsnCode,
            entity.Uom,
            entity.TaxRate,
            entity.ItemType,
            entity.Resolution,
            entity.BitrateMbps,
            entity.WarrantyValue,
            entity.WarrantyUnit,
            entity.Notes,
            entity.Images
                .OrderBy(image => image.SortOrder)
                .Select(ToDto)
                .ToList(),
            entity.CreatedAt,
            entity.UpdatedAt);

    internal static CameraImageDto ToDto(CameraImage image) =>
        new(image.Id, image.FileName, image.ContentType, image.SizeBytes, image.SortOrder, ImageUrl(image.Id));
}
