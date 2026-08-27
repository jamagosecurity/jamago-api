using FluentValidation;

namespace Jama.Application.Cameras.Commands.UpdateCamera;

public sealed class UpdateCameraCommandValidator : AbstractValidator<UpdateCameraCommand>
{
    public UpdateCameraCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("A camera id is required.");
        CameraWriteRules.Apply(this);
    }
}
