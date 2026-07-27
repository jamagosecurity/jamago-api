using FluentValidation;

namespace Jama.Application.Auth.Commands.ChangePassword;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(v => v.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        // Mirrors CreateStaffCommandValidator so a self-service change cannot
        // weaken a password below the strength admins are held to.
        RuleFor(v => v.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must include an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must include a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must include a number.")
            .NotEqual(v => v.CurrentPassword)
                .WithMessage("New password must be different from your current password.");
    }
}
