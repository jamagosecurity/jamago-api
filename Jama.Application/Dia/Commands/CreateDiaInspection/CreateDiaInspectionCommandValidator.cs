using FluentValidation;

namespace Jama.Application.Dia.Commands.CreateDiaInspection;

public sealed class CreateDiaInspectionCommandValidator : AbstractValidator<CreateDiaInspectionCommand>
{
    public CreateDiaInspectionCommandValidator()
    {
        RuleFor(x => x.DiaNumber)
            .NotEmpty().WithMessage("DIA number is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("DIA number cannot be blank.")
            .MaximumLength(100).WithMessage("DIA number must not exceed 100 characters.");

        RuleFor(x => x.ClientNumber)
            .NotEmpty().WithMessage("Client number is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Client number cannot be blank.")
            .MaximumLength(100).WithMessage("Client number must not exceed 100 characters.");

        RuleFor(x => x.ClientName)
            .NotEmpty().WithMessage("Client name is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Client name cannot be blank.")
            .MaximumLength(200).WithMessage("Client name must not exceed 200 characters.");

        RuleFor(x => x.ClientLocation)
            .NotEmpty().WithMessage("Client location is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Client location cannot be blank.")
            .MaximumLength(300).WithMessage("Client location must not exceed 300 characters.");
    }
}
