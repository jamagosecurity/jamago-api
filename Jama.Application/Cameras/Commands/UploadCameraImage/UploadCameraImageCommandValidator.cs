using FluentValidation;

namespace Jama.Application.Cameras.Commands.UploadCameraImage;

public sealed class UploadCameraImageCommandValidator : AbstractValidator<UploadCameraImageCommand>
{
    /// <summary>Product photos, not source files — 5 MB is generous for a listing.</summary>
    private const long MaxBytes = 5L * 1024 * 1024;

    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public UploadCameraImageCommandValidator()
    {
        RuleFor(x => x.CameraId).NotEmpty().WithMessage("A camera id is required.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("A file name is required.")
            // Path.GetFileName strips any directory portion a client tried to
            // send; if nothing survives, the name was only a path.
            .Must(name => !string.IsNullOrWhiteSpace(Path.GetFileName(name)))
            .WithMessage("A file name is required.")
            .MaximumLength(300).WithMessage("File name must be 300 characters or fewer.");

        RuleFor(x => x.FileName)
            .Must(name => AllowedExtensions.Contains(
                Path.GetExtension(Path.GetFileName(name)).ToLowerInvariant()))
            .WithMessage($"Accepted image types: {string.Join(", ", AllowedExtensions)}.")
            .When(x => !string.IsNullOrWhiteSpace(Path.GetFileName(x.FileName)));

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0).WithMessage("The image is empty.")
            .LessThanOrEqualTo(MaxBytes).WithMessage("Images must be 5 MB or smaller.");
    }
}
