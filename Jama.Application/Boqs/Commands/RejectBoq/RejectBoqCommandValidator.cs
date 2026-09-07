using FluentValidation;

namespace Jama.Application.Boqs.Commands.RejectBoq;

public sealed class RejectBoqCommandValidator : AbstractValidator<RejectBoqCommand>
{
    /// <summary>Long enough to say what is wrong, short enough to stay a reason
    /// rather than becoming the reworked quotation.</summary>
    private const int ReasonMaxLength = 1000;

    /// <summary>"No" and "wrong" are not reasons anyone can act on. Low enough
    /// not to argue with a terse approver, high enough to stop a keystroke.</summary>
    private const int ReasonMinLength = 5;

    public RejectBoqCommandValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Give a reason for rejecting this quotation.")
            .MinimumLength(ReasonMinLength)
            .WithMessage($"The reason must be at least {ReasonMinLength} characters.")
            .MaximumLength(ReasonMaxLength)
            .WithMessage($"The reason must be {ReasonMaxLength} characters or fewer.");
    }
}
