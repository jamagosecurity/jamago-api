using FluentValidation;

namespace Jama.Application.Boqs.Commands.UpdateBoqContractText;

public sealed class UpdateBoqContractTextCommandValidator : AbstractValidator<UpdateBoqContractTextCommand>
{
    public UpdateBoqContractTextCommandValidator()
    {
        RuleFor(v => v.ContractText)
            .MaximumLength(12000).WithMessage("Contract text must be 12,000 characters or fewer.");
    }
}
