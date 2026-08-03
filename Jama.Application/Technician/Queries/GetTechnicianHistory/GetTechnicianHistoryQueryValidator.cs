using FluentValidation;

namespace Jama.Application.Technician.Queries.GetTechnicianHistory;

/// <summary>
/// Added while splitting this feature up: the equivalent DIA history query was
/// validated and this one was not, so an out-of-range pageSize was silently
/// clamped here but rejected there for the same input.
/// </summary>
public sealed class GetTechnicianHistoryQueryValidator : AbstractValidator<GetTechnicianHistoryQuery>
{
    public GetTechnicianHistoryQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0).WithMessage("pageNumber must be greater than 0.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("pageSize must be between 1 and 100.");
    }
}
