using FluentValidation;

namespace Jama.Application.Cameras.Commands.CreateCamera;

public sealed class CreateCameraCommandValidator : AbstractValidator<CreateCameraCommand>
{
    public CreateCameraCommandValidator() => CameraWriteRules.Apply(this);
}
