using FluentValidation;

namespace Jama.Application.Dia.Queries.GetDiaHistory;

public sealed class GetDiaHistoryQueryValidator : AbstractValidator<GetDiaHistoryQuery>
{
    public GetDiaHistoryQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0).WithMessage("pageNumber must be greater than 0.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("pageSize must be between 1 and 100.");

        RuleFor(x => x)
            .Must(x => !x.FromDate.HasValue || !x.ToDate.HasValue || x.FromDate <= x.ToDate)
            .WithMessage("fromDate must be before or equal to toDate.");
    }
}
