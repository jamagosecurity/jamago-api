using FluentValidation;

namespace Jama.Application.Auth.Queries.GetCurrentUser;

public class GetCurrentUserQueryValidator : AbstractValidator<GetCurrentUserQuery>
{
    public GetCurrentUserQueryValidator()
    {
        RuleFor(v => v.UserId)
            .NotEmpty().WithMessage("User id is required.");
    }
}
