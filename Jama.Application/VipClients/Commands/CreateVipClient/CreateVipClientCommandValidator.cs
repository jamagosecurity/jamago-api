using FluentValidation;
using Jama.Application.Common;

namespace Jama.Application.VipClients.Commands.CreateVipClient;

public sealed class CreateVipClientCommandValidator : AbstractValidator<CreateVipClientCommand>
{
    public CreateVipClientCommandValidator()
    {
        RuleFor(v => v.ClientName)
            .NotEmpty().WithMessage("Client name is required.")
            .MaximumLength(200).WithMessage("Client name must be 200 characters or fewer.");

        RuleFor(v => v.ProjectName)
            .NotEmpty().WithMessage("Project name is required.")
            .MaximumLength(200).WithMessage("Project name must be 200 characters or fewer.");

        RuleFor(v => v.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Enter a valid email address.")
            .MaximumLength(200).WithMessage("Email must be 200 characters or fewer.");

        RuleFor(v => v.FolderName)
            .MaximumLength(400).WithMessage("Folder name must be 400 characters or fewer.");

        RuleFor(v => v.Password).NotEmpty().WithMessage("Password is required.");
        PasswordRules.Strong(RuleFor(v => v.Password))
            .When(v => !string.IsNullOrEmpty(v.Password));
    }
}
