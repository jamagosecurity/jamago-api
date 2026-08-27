using FluentValidation;

namespace Jama.Application.Quotations.Commands.UpdateQuotation;

public sealed class UpdateQuotationCommandValidator : AbstractValidator<UpdateQuotationCommand>
{
    public UpdateQuotationCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("A quotation id is required.");
        QuotationWriteRules.Apply(this);
    }
}
