using FluentValidation;

namespace Jama.Application.Dia.Queries.GetDiaInspections;

public sealed class GetDiaInspectionsQueryValidator : AbstractValidator<GetDiaInspectionsQuery>
{
    /// <summary>
    /// Allow-list rather than reflection over the entity: sortBy arrives from
    /// the query string, and anything not on this list is rejected before it
    /// can reach the sort switch.
    /// </summary>
    private static readonly string[] SortFields =
    [
        "createdDate", "updatedDate", "diaNumber", "clientNumber", "clientName",
        "clientLocation", "activatedDate", "currentQuarter", "nextInspectionDate", "status",
    ];

    public GetDiaInspectionsQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0).WithMessage("pageNumber must be greater than 0.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("pageSize must be between 1 and 100.");

        RuleFor(x => x.SortBy).Must(x => string.IsNullOrWhiteSpace(x)
            || SortFields.Contains(x, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"sortBy must be one of: {string.Join(", ", SortFields)}.");

        RuleFor(x => x.SortDirection).Must(x => string.IsNullOrWhiteSpace(x)
            || x.Equals("asc", StringComparison.OrdinalIgnoreCase)
            || x.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("sortDirection must be asc or desc.");
    }
}
