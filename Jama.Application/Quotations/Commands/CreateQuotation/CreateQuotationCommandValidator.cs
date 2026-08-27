using FluentValidation;

namespace Jama.Application.Quotations.Commands.CreateQuotation;

public sealed class CreateQuotationCommandValidator : AbstractValidator<CreateQuotationCommand>
{
    public CreateQuotationCommandValidator() => QuotationWriteRules.Apply(this);
}
