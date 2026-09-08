using FluentValidation;

namespace Jama.Application.Drawings;

/// <summary>
/// The metadata fields Create and Update share. A drawing has no nested
/// structure the way a quotation has sections and lines — its content is its
/// files, uploaded separately — so this is scalar validation only, shared
/// between the two commands the same way IBoqWrite shares BoqWriteRules.
/// </summary>
public interface IDrawingWrite
{
    string? ProjectName { get; }
    string? ClientName { get; }
    string? SiteLocation { get; }
    string? ContactNumber { get; }
    string? Notes { get; }
}

internal static class DrawingWriteRules
{
    internal const int NameMaxLength = 200;
    internal const int ContactMaxLength = 40;
    internal const int NotesMaxLength = 2000;

    internal static void Apply<T>(AbstractValidator<T> validator) where T : IDrawingWrite
    {
        validator.RuleFor(x => x.ProjectName)
            .NotEmpty().WithMessage("Project name is required.")
            .MaximumLength(NameMaxLength)
            .WithMessage($"Project name must be {NameMaxLength} characters or fewer.");

        validator.RuleFor(x => x.ClientName).MaximumLength(NameMaxLength);
        validator.RuleFor(x => x.SiteLocation).MaximumLength(NameMaxLength);
        validator.RuleFor(x => x.ContactNumber).MaximumLength(ContactMaxLength)
            .WithMessage($"Contact number must be {ContactMaxLength} characters or fewer.");
        validator.RuleFor(x => x.Notes).MaximumLength(NotesMaxLength);
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>Copies the scalar fields onto the entity. Called by both Create
    /// and Update, after each has decided whether the write is currently
    /// allowed.</summary>
    internal static void Apply(Jama.Domain.Entities.Drawing entity, IDrawingWrite request)
    {
        entity.ProjectName = request.ProjectName?.Trim() ?? string.Empty;
        entity.ClientName = Clean(request.ClientName);
        entity.SiteLocation = Clean(request.SiteLocation);
        entity.ContactNumber = Clean(request.ContactNumber);
        entity.Notes = Clean(request.Notes);
    }
}
