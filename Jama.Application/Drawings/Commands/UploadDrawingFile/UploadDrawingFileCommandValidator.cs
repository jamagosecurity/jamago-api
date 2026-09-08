using FluentValidation;
using Jama.Application.Options;
using Microsoft.Extensions.Options;

namespace Jama.Application.Drawings.Commands.UploadDrawingFile;

/// <summary>
/// Mirrors UploadVipDocumentCommandValidator, against the drawing-specific
/// limits — DrawingMaxFileSizeMb and DrawingAllowedExtensions — rather than the
/// shared ones every other upload in the app uses.
/// </summary>
public sealed class UploadDrawingFileCommandValidator : AbstractValidator<UploadDrawingFileCommand>
{
    public UploadDrawingFileCommandValidator(IOptions<FileStorageSettings> options)
    {
        var settings = options.Value;
        var maxBytes = settings.DrawingMaxFileSizeMb * 1024L * 1024L;

        RuleFor(x => x.DrawingId).NotEmpty().WithMessage("A drawing id is required.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("A file name is required.")
            .Must(name => !string.IsNullOrWhiteSpace(Path.GetFileName(name)))
            .WithMessage("A file name is required.")
            .MaximumLength(400).WithMessage("File name must not exceed 400 characters.");

        RuleFor(x => x.FileName)
            .Must(name => settings.DrawingAllowedExtensions.Contains(
                Path.GetExtension(Path.GetFileName(name)).ToLowerInvariant()))
            .WithMessage($"Accepted file types: {string.Join(", ", settings.DrawingAllowedExtensions)}.")
            .When(x => !string.IsNullOrWhiteSpace(Path.GetFileName(x.FileName)));

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0).WithMessage("The file is empty.")
            .LessThanOrEqualTo(maxBytes)
            .WithMessage($"Files must be {settings.DrawingMaxFileSizeMb} MB or smaller.");
    }
}
