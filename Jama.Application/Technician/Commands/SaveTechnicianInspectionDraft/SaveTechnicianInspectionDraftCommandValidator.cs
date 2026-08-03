using FluentValidation;

namespace Jama.Application.Technician.Commands.SaveTechnicianInspectionDraft;

public sealed class SaveTechnicianInspectionDraftCommandValidator
    : AbstractValidator<SaveTechnicianInspectionDraftCommand>
{
    public SaveTechnicianInspectionDraftCommandValidator()
    {
        RuleFor(x => x.InspectionId).NotEmpty().WithMessage("An inspection id is required.");

        // Only the camera rows are checked. The other sections are free-text
        // notes a technician fills in progressively, and a draft save must not
        // block on a half-finished form.
        RuleForEach(x => x.Cameras).ChildRules(camera =>
        {
            camera.RuleFor(x => x.Brand)
                .NotEmpty().WithMessage("Camera brand is required.")
                .MaximumLength(120).WithMessage("Camera brand must not exceed 120 characters.");
            camera.RuleFor(x => x.Model)
                .NotEmpty().WithMessage("Camera model is required.")
                .MaximumLength(120).WithMessage("Camera model must not exceed 120 characters.");
            camera.RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("Camera quantity must be greater than 0.");
        });
    }
}
