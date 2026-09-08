using FluentValidation;

namespace Jama.Application.Drawings.Commands.RejectDrawing;

public sealed class RejectDrawingCommandValidator : AbstractValidator<RejectDrawingCommand>
{
    private const int ReasonMaxLength = 1000;
    private const int ReasonMinLength = 5;

    public RejectDrawingCommandValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Give a reason for rejecting this drawing.")
            .MinimumLength(ReasonMinLength)
            .WithMessage($"The reason must be at least {ReasonMinLength} characters.")
            .MaximumLength(ReasonMaxLength)
            .WithMessage($"The reason must be {ReasonMaxLength} characters or fewer.");
    }
}
