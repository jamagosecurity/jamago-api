using FluentValidation;
using Jama.Domain.Entities;
using Jama.Domain.Enums;

namespace Jama.Application.Cameras;

/// <summary>
/// The writable shape of a stock item, implemented by both the create and update
/// commands.
///
/// With roughly twenty fields, having each command carry its own validator and
/// its own copy of the assignment code was the obvious way for the two to drift:
/// a rule tightened on create and forgotten on update is invisible until someone
/// edits their way past it. One interface, one rule set, one writer.
/// </summary>
public interface ICameraWrite
{
    string? ItemName { get; }
    string? Brand { get; }
    CameraType Type { get; }
    string? ModelNo { get; }
    ProductCategory Category { get; }
    string? SearchKey { get; }
    string? DescriptionEn { get; }
    string? DescriptionAr { get; }

    decimal? SupplierCost { get; }
    decimal? Margin { get; }
    decimal? Rate { get; }
    decimal? Discount { get; }
    int Quantity { get; }
    int? LowStock { get; }
    string? HsnCode { get; }
    UnitOfMeasurement Uom { get; }
    CameraResolution Resolution { get; }
    decimal? BitrateMbps { get; }
    decimal? TaxRate { get; }
    ItemType ItemType { get; }

    int? WarrantyValue { get; }
    WarrantyUnit? WarrantyUnit { get; }
    string? Notes { get; }
}

internal static class CameraWriteRules
{
    /// <summary>Applies every field rule to a create or update validator.</summary>
    internal static void Apply<T>(AbstractValidator<T> validator) where T : ICameraWrite
    {
        CameraFieldRules.ItemName(validator.RuleFor(x => x.ItemName));
        CameraFieldRules.Brand(validator.RuleFor(x => x.Brand));
        CameraFieldRules.Type(validator.RuleFor(x => x.Type));
        CameraFieldRules.ModelNo(validator.RuleFor(x => x.ModelNo));
        CameraFieldRules.Category(validator.RuleFor(x => x.Category));
        CameraFieldRules.SearchKey(validator.RuleFor(x => x.SearchKey));
        CameraFieldRules.Description(validator.RuleFor(x => x.DescriptionEn), "English");
        CameraFieldRules.Description(validator.RuleFor(x => x.DescriptionAr), "Arabic");

        // The money and percentage rules only run when a value was supplied —
        // every one of these fields is optional, and NotNull would make the whole
        // pricing block mandatory just to save a name and a quantity.
        CameraFieldRules.Money(validator.RuleFor(x => x.SupplierCost), "Supplier cost")
            .When(x => x.SupplierCost.HasValue);
        CameraFieldRules.Money(validator.RuleFor(x => x.Rate), "Rate")
            .When(x => x.Rate.HasValue);
        // Margin can exceed 100% — a 4x mark-up is 300% — but discount and tax
        // are shares of a price and cannot.
        CameraFieldRules.Percent(validator.RuleFor(x => x.Margin), "Margin", 10_000m)
            .When(x => x.Margin.HasValue);
        CameraFieldRules.Percent(validator.RuleFor(x => x.Discount), "Discount", 100m)
            .When(x => x.Discount.HasValue);
        CameraFieldRules.Percent(validator.RuleFor(x => x.TaxRate), "Tax", 100m)
            .When(x => x.TaxRate.HasValue);

        CameraFieldRules.Quantity(validator.RuleFor(x => x.Quantity));
        CameraFieldRules.LowStock(validator.RuleFor(x => x.LowStock)).When(x => x.LowStock.HasValue);
        CameraFieldRules.HsnCode(validator.RuleFor(x => x.HsnCode));
        CameraFieldRules.Uom(validator.RuleFor(x => x.Uom));
        CameraFieldRules.ItemTypeRule(validator.RuleFor(x => x.ItemType));

        CameraFieldRules.WarrantyValue(validator.RuleFor(x => x.WarrantyValue))
            .When(x => x.WarrantyValue.HasValue);
        CameraFieldRules.WarrantyUnitRule(validator.RuleFor(x => x.WarrantyUnit))
            .When(x => x.WarrantyUnit.HasValue);

        // A period and a unit are only meaningful together: "12" with no unit
        // does not say twelve of what, and "Month" alone says nothing at all.
        validator.RuleFor(x => x.WarrantyUnit)
            .NotNull().WithMessage("Choose a warranty unit — day, month or year.")
            .When(x => x.WarrantyValue.HasValue);

        validator.RuleFor(x => x.WarrantyValue)
            .NotNull().WithMessage("Enter how long the warranty lasts.")
            .When(x => x.WarrantyUnit.HasValue);

        validator.RuleFor(x => x.Resolution).IsInEnum().WithMessage("Select a valid resolution.");

        // Upper bound is a typo guard, not a technical limit: 100 Mbps is far
        // above any single-camera profile, so a value past it is a slipped
        // decimal point rather than a real stream.
        validator.RuleFor(x => x.BitrateMbps)
            .GreaterThan(0m).WithMessage("Bitrate must be greater than 0.")
            .LessThanOrEqualTo(100m).WithMessage("Bitrate must be 100 Mbps or less.")
            .When(x => x.BitrateMbps.HasValue);

        CameraFieldRules.Notes(validator.RuleFor(x => x.Notes));
    }
}

internal static class CameraWriter
{
    /// <summary>
    /// Copies a create or update request onto the entity. Normalisation lives
    /// here so both paths trim, blank-to-null and round identically.
    /// </summary>
    internal static void Apply(Camera entity, ICameraWrite request)
    {
        entity.ItemName = request.ItemName?.Trim() ?? string.Empty;
        entity.Brand = CameraRules.NormalizeBrand(request.Brand);
        entity.Type = request.Type;
        entity.ModelNo = CameraRules.NormalizeModelNo(request.ModelNo);
        entity.Category = request.Category;
        entity.SearchKey = CameraRules.NormalizeOptional(request.SearchKey);
        entity.DescriptionEn = CameraRules.NormalizeOptional(request.DescriptionEn);
        entity.DescriptionAr = CameraRules.NormalizeOptional(request.DescriptionAr);

        // Rounded to the precision the columns actually hold, so what comes back
        // in the response is what was stored rather than the unrounded input.
        entity.SupplierCost = Round(request.SupplierCost, 2);
        entity.Margin = Round(request.Margin, 2);
        entity.Rate = Round(request.Rate, 2);
        entity.Discount = Round(request.Discount, 2);
        entity.TaxRate = Round(request.TaxRate, 2);

        entity.Quantity = request.Quantity;
        entity.LowStock = request.LowStock;
        entity.HsnCode = CameraRules.NormalizeOptional(request.HsnCode);
        entity.Uom = request.Uom;
        entity.Resolution = request.Resolution;
        entity.BitrateMbps = Round(request.BitrateMbps, 3);
        entity.ItemType = request.ItemType;

        entity.WarrantyValue = request.WarrantyValue;
        entity.WarrantyUnit = request.WarrantyUnit;
        entity.Notes = CameraRules.NormalizeOptional(request.Notes);
    }

    private static decimal? Round(decimal? value, int places) =>
        value.HasValue ? Math.Round(value.Value, places, MidpointRounding.AwayFromZero) : null;
}
