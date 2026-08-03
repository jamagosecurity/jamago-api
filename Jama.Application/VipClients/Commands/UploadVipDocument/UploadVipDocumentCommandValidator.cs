using FluentValidation;
using Jama.Application.Options;
using Microsoft.Extensions.Options;

namespace Jama.Application.VipClients.Commands.UploadVipDocument;

/// <summary>
/// The upload rules used to sit inline in the handler, which meant this one
/// command reported input problems differently from every other command in the
/// codebase — a rejected file came back as a handler failure rather than a
/// validation error, so the client saw a different response shape for the same
/// class of mistake.
///
/// Injecting IOptions here keeps the limits configurable while still running
/// them through the normal validation pipeline.
/// </summary>
public sealed class UploadVipDocumentCommandValidator : AbstractValidator<UploadVipDocumentCommand>
{
    public UploadVipDocumentCommandValidator(IOptions<FileStorageSettings> options)
    {
        var settings = options.Value;
        var maxBytes = settings.MaxFileSizeMb * 1024L * 1024L;

        RuleFor(x => x.FolderId).NotEmpty().WithMessage("A folder id is required.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("A file name is required.")
            // Path.GetFileName strips any directory portion a client tried to
            // send; if nothing survives, the name was only a path.
            .Must(name => !string.IsNullOrWhiteSpace(Path.GetFileName(name)))
            .WithMessage("A file name is required.")
            .MaximumLength(400).WithMessage("File name must not exceed 400 characters.");

        RuleFor(x => x.FileName)
            .Must(name => settings.AllowedExtensions.Contains(
                Path.GetExtension(Path.GetFileName(name)).ToLowerInvariant()))
            .WithMessage($"Accepted file types: {string.Join(", ", settings.AllowedExtensions)}.")
            .When(x => !string.IsNullOrWhiteSpace(Path.GetFileName(x.FileName)));

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0).WithMessage("The file is empty.")
            .LessThanOrEqualTo(maxBytes)
            .WithMessage($"Files must be {settings.MaxFileSizeMb} MB or smaller.");
    }
}
