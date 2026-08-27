using FluentValidation;
using Jama.Domain.Enums;

namespace Jama.Application.Cameras;

/// <summary>
/// The field rules for a stock item, shared by the create and update validators
/// so the two can never drift apart — the same shape of message the staff editor
/// gets from <c>PasswordRules</c>.
/// </summary>
internal static class CameraFieldRules
{
    internal const int ItemNameMaxLength = 200;
    internal const int BrandMaxLength = 120;
    internal const int ModelNoMaxLength = 120;
    internal const int SearchKeyMaxLength = 300;
    internal const int DescriptionMaxLength = 500;
    internal const int NotesMaxLength = 1000;
    internal const int HsnCodeMaxLength = 20;

    /// <summary>A sanity ceiling, not a business limit: it catches a mistyped
    /// quantity rather than capping real stock.</summary>
    internal const int QuantityMax = 100_000;

    /// <summary>Fits decimal(18,2) with room to spare; a stock item priced above
    /// this is a typo, not a sale.</summary>
    internal const decimal MoneyMax = 9_999_999.99m;

    internal static IRuleBuilderOptions<T, string?> ItemName<T>(IRuleBuilder<T, string?> rule) =>
        rule
            .NotEmpty().WithMessage("Item name is required.")
            .MaximumLength(ItemNameMaxLength)
            .WithMessage($"Item name must be {ItemNameMaxLength} characters or fewer.");

    internal static IRuleBuilderOptions<T, string?> Brand<T>(IRuleBuilder<T, string?> rule) =>
        rule
            .NotEmpty().WithMessage("Brand is required.")
            .MaximumLength(BrandMaxLength)
            .WithMessage($"Brand must be {BrandMaxLength} characters or fewer.");

    internal static IRuleBuilderOptions<T, CameraType> Type<T>(IRuleBuilder<T, CameraType> rule) =>
        rule.IsInEnum().WithMessage("Select a valid camera type.");

    internal static IRuleBuilderOptions<T, ProductCategory> Category<T>(IRuleBuilder<T, ProductCategory> rule) =>
        rule.IsInEnum().WithMessage("Select a valid product category.");

    internal static IRuleBuilderOptions<T, UnitOfMeasurement> Uom<T>(IRuleBuilder<T, UnitOfMeasurement> rule) =>
        rule.IsInEnum().WithMessage("Select a valid unit of measurement.");

    internal static IRuleBuilderOptions<T, ItemType> ItemTypeRule<T>(IRuleBuilder<T, ItemType> rule) =>
        rule.IsInEnum().WithMessage("Select a valid item type.");

    /// <summary>Optional — not every line has a model number to hand.</summary>
    internal static IRuleBuilderOptions<T, string?> ModelNo<T>(IRuleBuilder<T, string?> rule) =>
        rule
            .MaximumLength(ModelNoMaxLength)
            .WithMessage($"Model no must be {ModelNoMaxLength} characters or fewer.");

    internal static IRuleBuilderOptions<T, string?> SearchKey<T>(IRuleBuilder<T, string?> rule) =>
        rule
            .MaximumLength(SearchKeyMaxLength)
            .WithMessage($"Search key must be {SearchKeyMaxLength} characters or fewer.");

    internal static IRuleBuilderOptions<T, string?> HsnCode<T>(IRuleBuilder<T, string?> rule) =>
        rule
            .MaximumLength(HsnCodeMaxLength)
            .WithMessage($"HSN code must be {HsnCodeMaxLength} characters or fewer.");

    internal static IRuleBuilderOptions<T, string?> Notes<T>(IRuleBuilder<T, string?> rule) =>
        rule
            .MaximumLength(NotesMaxLength)
            .WithMessage($"Notes must be {NotesMaxLength} characters or fewer.");

    /// <summary>
    /// Both descriptions are optional and independent, so the language is named
    /// in the message — "Description must be 500 characters or fewer" would not
    /// say which of the two boxes to shorten.
    /// </summary>
    internal static IRuleBuilderOptions<T, string?> Description<T>(IRuleBuilder<T, string?> rule, string language) =>
        rule
            .MaximumLength(DescriptionMaxLength)
            .WithMessage($"Description ({language}) must be {DescriptionMaxLength} characters or fewer.");

    internal static IRuleBuilderOptions<T, int> Quantity<T>(IRuleBuilder<T, int> rule) =>
        rule
            .GreaterThanOrEqualTo(0).WithMessage("Quantity cannot be negative.")
            .LessThanOrEqualTo(QuantityMax)
            .WithMessage($"Quantity must be {QuantityMax:N0} or fewer.");

    internal static IRuleBuilderOptions<T, int?> LowStock<T>(IRuleBuilder<T, int?> rule) =>
        rule
            .GreaterThanOrEqualTo(0).WithMessage("Low stock cannot be negative.")
            .LessThanOrEqualTo(QuantityMax)
            .WithMessage($"Low stock must be {QuantityMax:N0} or fewer.");

    /// <summary>A money amount in QAR. Null is allowed — the field is optional.</summary>
    internal static IRuleBuilderOptions<T, decimal?> Money<T>(IRuleBuilder<T, decimal?> rule, string label) =>
        rule
            .GreaterThanOrEqualTo(0).WithMessage($"{label} cannot be negative.")
            .LessThanOrEqualTo(MoneyMax)
            .WithMessage($"{label} must be {MoneyMax:N2} or less.");

    /// <summary>A percentage. Margin may exceed 100; discount and tax may not.</summary>
    internal static IRuleBuilderOptions<T, decimal?> Percent<T>(IRuleBuilder<T, decimal?> rule, string label, decimal max) =>
        rule
            .GreaterThanOrEqualTo(0).WithMessage($"{label} cannot be negative.")
            .LessThanOrEqualTo(max).WithMessage($"{label} must be {max:N0}% or less.");

    internal static IRuleBuilderOptions<T, int?> WarrantyValue<T>(IRuleBuilder<T, int?> rule) =>
        rule
            .GreaterThan(0).WithMessage("Warranty period must be at least 1.")
            .LessThanOrEqualTo(999).WithMessage("Warranty period must be 999 or fewer.");

    internal static IRuleBuilderOptions<T, WarrantyUnit?> WarrantyUnitRule<T>(IRuleBuilder<T, WarrantyUnit?> rule) =>
        rule.IsInEnum().WithMessage("Select a valid warranty unit.");
}
