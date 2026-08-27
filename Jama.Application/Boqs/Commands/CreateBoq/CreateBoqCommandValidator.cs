using FluentValidation;

namespace Jama.Application.Boqs.Commands.CreateBoq;

public sealed class CreateBoqCommandValidator : AbstractValidator<CreateBoqCommand>
{
    public CreateBoqCommandValidator() => BoqWriteRules.Apply(this);
}
