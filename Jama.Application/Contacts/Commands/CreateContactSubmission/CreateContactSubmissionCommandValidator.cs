using FluentValidation;

namespace Jama.Application.Contacts.Commands.CreateContactSubmission;

public class CreateContactSubmissionCommandValidator : AbstractValidator<CreateContactSubmissionCommand>
{
    public CreateContactSubmissionCommandValidator()
    {
        RuleFor(v => v.FullName)
            .NotEmpty().WithMessage("Full Name is required.")
            .MaximumLength(150).WithMessage("Full Name must not exceed 150 characters.");

        RuleFor(v => v.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not valid.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");

        RuleFor(v => v.Phone)
            .MaximumLength(30).WithMessage("Phone must not exceed 30 characters.");

        RuleFor(v => v.Service)
            .NotEmpty().WithMessage("Service is required.")
            .MaximumLength(120).WithMessage("Service must not exceed 120 characters.");

        RuleFor(v => v.Message)
            .NotEmpty().WithMessage("Message is required.");
    }
}
