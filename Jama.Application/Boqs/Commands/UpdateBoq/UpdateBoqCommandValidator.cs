using FluentValidation;

namespace Jama.Application.Boqs.Commands.UpdateBoq;

public sealed class UpdateBoqCommandValidator : AbstractValidator<UpdateBoqCommand>
{
    public UpdateBoqCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("A BOQ id is required.");
        BoqWriteRules.Apply(this);
    }
}
