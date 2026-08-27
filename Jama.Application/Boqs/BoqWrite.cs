using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Boqs;

/// <summary>
/// A line as the client may specify it: which catalogue item, and how many.
///
/// Note what is NOT here — no name, no unit, and above all no rate. Those are
/// read from the catalogue by the server. Accepting a rate from the client would
/// make "staff cannot change prices" a rule the UI merely chooses to honour,
/// which is not a rule at all.
/// </summary>
public sealed record BoqLineInput
{
    public Guid CameraId { get; init; }
    public decimal Quantity { get; init; }
}

public sealed record BoqSectionInput
{
    public string? Title { get; init; }
    public IReadOnlyList<BoqLineInput> Lines { get; init; } = [];
}

public interface IBoqWrite
{
    string? ProjectName { get; }
    string? SiteLocation { get; }
    string? ClientName { get; }
    string? ContactNumber { get; }
    DateOnly? IssueDate { get; }
    BoqStatus Status { get; }
    string? Notes { get; }
    IReadOnlyList<BoqSectionInput> Sections { get; }
}

internal static class BoqWriteRules
{
    internal const int NameMaxLength = 200;
    internal const int ContactMaxLength = 40;
    internal const int NotesMaxLength = 2000;
    internal const int MaxSections = 40;
    internal const int MaxLinesPerSection = 200;
    internal const decimal QuantityMax = 1_000_000m;

    internal static void Apply<T>(AbstractValidator<T> validator) where T : IBoqWrite
    {
        validator.RuleFor(x => x.ProjectName)
            .NotEmpty().WithMessage("Project name is required.")
            .MaximumLength(NameMaxLength)
            .WithMessage($"Project name must be {NameMaxLength} characters or fewer.");

        validator.RuleFor(x => x.SiteLocation).MaximumLength(NameMaxLength);
        validator.RuleFor(x => x.ClientName).MaximumLength(NameMaxLength);
        validator.RuleFor(x => x.ContactNumber).MaximumLength(ContactMaxLength)
            .WithMessage($"Contact number must be {ContactMaxLength} characters or fewer.");
        validator.RuleFor(x => x.Notes).MaximumLength(NotesMaxLength);
        validator.RuleFor(x => x.Status).IsInEnum().WithMessage("Select a valid status.");

        validator.RuleFor(x => x.Sections)
            .NotEmpty().WithMessage("Add at least one section.")
            .Must(sections => sections.Count <= MaxSections)
            .WithMessage($"A BOQ cannot have more than {MaxSections} sections.");

        validator.RuleForEach(x => x.Sections).ChildRules(section =>
        {
            section.RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Every section needs a title.")
                .Must(BoqSectionTitles.IsAllowed)
                .WithMessage("Choose a section title from the list.");

            section.RuleFor(x => x.Lines)
                .NotEmpty().WithMessage("Every section needs at least one line.")
                .Must(lines => lines.Count <= MaxLinesPerSection)
                .WithMessage($"A section cannot have more than {MaxLinesPerSection} lines.");

            section.RuleForEach(x => x.Lines).ChildRules(line =>
            {
                line.RuleFor(x => x.CameraId)
                    .NotEmpty().WithMessage("Every line must point at a stock item.");

                line.RuleFor(x => x.Quantity)
                    .GreaterThan(0).WithMessage("Line quantity must be greater than 0.")
                    .LessThanOrEqualTo(QuantityMax)
                    .WithMessage($"Line quantity must be {QuantityMax:N0} or fewer.");
            });
        });
    }
}

internal static class BoqWriter
{
    /// <summary>
    /// Copies the scalar fields onto the entity and BUILDS its sections, which it
    /// returns rather than attaching. How they are attached differs between
    /// create and update, and that is the caller's business.
    ///
    /// Every line's name, model, brand, unit and rate come from the catalogue row
    /// the client named — never from the request. A line naming an item that does
    /// not exist is refused rather than written with blanks.
    /// </summary>
    internal static async Task<(string? Error, List<BoqSection> Sections)> BuildAsync(
        Boq boq,
        IBoqWrite request,
        IApplicationDbContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Every referenced item fetched once, rather than per line.
        var wanted = request.Sections
            .SelectMany(s => s.Lines)
            .Select(l => l.CameraId)
            .Distinct()
            .ToList();

        var catalogue = await context.Cameras
            .AsNoTracking()
            .Where(x => wanted.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        if (wanted.Any(id => !catalogue.ContainsKey(id)))
            return ("A line refers to a stock item that no longer exists. Remove it and try again.", []);

        boq.ProjectName = request.ProjectName?.Trim() ?? string.Empty;
        boq.SiteLocation = Clean(request.SiteLocation);
        boq.ClientName = Clean(request.ClientName);
        boq.ContactNumber = Clean(request.ContactNumber);
        boq.IssueDate = request.IssueDate ?? DateOnly.FromDateTime(now);
        boq.Status = request.Status;
        boq.Notes = Clean(request.Notes);

        var sections = new List<BoqSection>();
        var sectionOrder = 0;

        foreach (var input in request.Sections)
        {
            var section = new BoqSection
            {
                Id = Guid.CreateVersion7(),
                BoqId = boq.Id,
                Title = BoqSectionTitles.Canonical(input.Title),
                SortOrder = sectionOrder++,
                CreatedAt = now,
            };

            var lineOrder = 0;
            foreach (var line in input.Lines)
            {
                var item = catalogue[line.CameraId];

                section.Lines.Add(new BoqLine
                {
                    Id = Guid.CreateVersion7(),
                    BoqSectionId = section.Id,
                    CameraId = item.Id,
                    ItemName = item.ItemName,
                    ModelNo = string.IsNullOrWhiteSpace(item.ModelNo) ? null : item.ModelNo,
                    Brand = item.Brand,
                    Uom = item.Uom,
                    Quantity = line.Quantity,
                    // The whole point: the rate is the catalogue's, not the caller's.
                    UnitRate = item.Rate ?? 0m,
                    SortOrder = lineOrder++,
                    CreatedAt = now,
                });
            }

            sections.Add(section);
        }

        boq.Total = Total(sections);
        return (null, sections);
    }

    /// <summary>Rounds each line to fils before summing, so the printed lines add
    /// up to the printed total.</summary>
    private static decimal Total(List<BoqSection> sections)
    {
        decimal total = 0m;
        foreach (var line in sections.SelectMany(s => s.Lines))
        {
            line.LineTotal = BoqMath.Round(line.Quantity * line.UnitRate);
            total += line.LineTotal;
        }
        return total;
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
