using FluentValidation;

namespace Jama.Application.Staffs.Commands.UpdateMyStaffProfile;

public class UpdateMyStaffProfileCommandValidator : AbstractValidator<UpdateMyStaffProfileCommand>
{
    public UpdateMyStaffProfileCommandValidator()
    {
        RuleFor(v => v.FullName)
            .NotEmpty().WithMessage("Full Name is required.")
            .MaximumLength(150).WithMessage("Full Name must not exceed 150 characters.");

        RuleFor(v => v.Responsibility)
            .MaximumLength(1000).WithMessage("Responsibility must not exceed 1000 characters.");
    }
}
