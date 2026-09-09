using FluentValidation;

namespace Jama.Application.Boqs.Commands.ApproveBoq;

public sealed class ApproveBoqCommandValidator : AbstractValidator<ApproveBoqCommand>
{
    /// <summary>Same ceiling as a rejection's reason — long enough for a real
    /// note, short enough to stay a note.</summary>
    private const int NoteMaxLength = 1000;

    public ApproveBoqCommandValidator()
    {
        // No NotEmpty, no minimum — unlike RejectBoqCommandValidator's reason,
        // an approval needs no justification to be valid.
        RuleFor(x => x.Note)
            .MaximumLength(NoteMaxLength)
            .WithMessage($"The note must be {NoteMaxLength} characters or fewer.");
    }
}
