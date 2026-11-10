using FluentValidation;
using Jama.Domain.Entities;

namespace Jama.Application.Maintenance;

/// <summary>Turns the lines a caller sent into priced entities — the one
/// place Create and Update agree on how a fee is decided.</summary>
internal static class MaintenanceLines
{
    internal const int MaxLines = 50;
    internal const int MaxQuantity = 999;
    internal const decimal DiscountMax = 9_999_999.99m;

    /// <param name="canSetFee">Whether the caller holds maintenance.manage.
    /// Without it, every line is charged at the catalogue price no matter what
    /// UnitFee was sent — a client cannot name their own price, even by
    /// editing the request by hand.</param>
    internal static List<MaintenanceRequestLine> Build(
        IReadOnlyList<MaintenanceLineInput> inputs,
        bool canSetFee,
        IReadOnlyCollection<MaintenanceCatalogueItem> catalogue) =>
        inputs.Select((input, index) =>
        {
            var issueType = input.IssueType!.Trim();
            var category = MaintenanceCategories.Normalize(input.Category, catalogue.Select(c => c.Category));
            var listed = MaintenanceIssueCatalogue.Find(category, issueType, catalogue);
            var catalogueFee = listed?.Price ?? MaintenanceIssueCatalogue.StandardFee;
            var unitFee = canSetFee && input.UnitFee is { } fee ? fee : catalogueFee;

            return new MaintenanceRequestLine
            {
                Id = Guid.CreateVersion7(),
                Category = category,
                IssueType = issueType,
                DescriptionEn = listed?.DescriptionEn,
                DescriptionAr = listed?.DescriptionAr,
                Quantity = input.Quantity,
                UnitFee = unitFee,
                CatalogueFee = catalogueFee,
                LineTotal = unitFee * input.Quantity,
                SortOrder = index,
            };
        }).ToList();

    internal static decimal Total(IEnumerable<MaintenanceRequestLine> lines) => lines.Sum(l => l.LineTotal);

    /// <summary>
    /// Sets the ticket's subtotal, discount and amount payable from its lines.
    /// The discount counts only from maintenance.manage — anyone else's is
    /// dropped, the same as a fee override. Returns an error, and changes
    /// nothing, when the discount is more than the lines come to: the fees are
    /// decided here, not by the caller, so this is the first point where the
    /// subtotal is actually known.
    /// </summary>
    internal static string? ApplyTotals(
        MaintenanceRequest ticket, IEnumerable<MaintenanceRequestLine> lines, decimal? discount, bool canSetFee)
    {
        var subtotal = Total(lines);
        var applied = canSetFee ? discount ?? 0m : 0m;

        if (applied > subtotal)
            return $"A discount of {applied:N2} QAR is more than the ticket total of {subtotal:N2} QAR.";

        ticket.Subtotal = subtotal;
        ticket.Discount = applied;
        ticket.ServiceFee = subtotal - applied;
        return null;
    }

    /// <summary>Category order first, then the order the lines were entered —
    /// the same grouping the editor shows them in.</summary>
    internal static IEnumerable<MaintenanceRequestLine> Ordered(IEnumerable<MaintenanceRequestLine> lines) =>
        lines.OrderBy(l => MaintenanceCategories.Rank(l.Category)).ThenBy(l => l.Category).ThenBy(l => l.SortOrder);
}

internal sealed class MaintenanceLineInputValidator : AbstractValidator<MaintenanceLineInput>
{
    public MaintenanceLineInputValidator()
    {
        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Every line needs a category.")
            .MaximumLength(MaintenanceCategories.MaxLength);
        RuleFor(x => x.IssueType)
            .NotEmpty().WithMessage("Every line needs an issue.")
            .MaximumLength(200);
        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, MaintenanceLines.MaxQuantity)
            .WithMessage($"Quantity must be between 1 and {MaintenanceLines.MaxQuantity}.");
        RuleFor(x => x.UnitFee)
            .GreaterThanOrEqualTo(0).WithMessage("A fee cannot be negative.")
            .When(x => x.UnitFee.HasValue);
    }
}
