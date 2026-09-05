using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Boqs;

/// <summary>
/// A line as the client may specify it: which catalogue item, how many, and —
/// for a caller allowed to set one — at what rate.
///
/// The name, model, brand and unit are still not here. Those are read from the
/// catalogue by the server, because they describe the item rather than the deal.
/// </summary>
public sealed record BoqLineInput
{
    public Guid CameraId { get; init; }
    public decimal Quantity { get; init; }

    /// <summary>
    /// A rate to use instead of the catalogue's. NULL means "price it from the
    /// catalogue", which is what an ordinary line sends.
    ///
    /// Honoured only for a caller holding boq.price; from anyone else a value
    /// that differs from the catalogue is REFUSED rather than quietly dropped.
    /// Silently ignoring it would show the preparer one price, print another,
    /// and give them no way to tell which they had agreed to.
    /// </summary>
    public decimal? UnitRate { get; init; }
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
    internal const decimal RateMax = 10_000_000m;

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

                // Zero is allowed: an item thrown in free is a normal thing to
                // quote, and forcing a penny onto it would misstate the offer.
                // Negative is not — a line that subtracts from the total is a
                // discount pretending to be equipment.
                line.RuleFor(x => x.UnitRate)
                    .GreaterThanOrEqualTo(0m).WithMessage("A rate cannot be negative.")
                    .LessThanOrEqualTo(RateMax)
                    .WithMessage($"A rate must be {RateMax:N0} or less.")
                    .When(x => x.UnitRate.HasValue);
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
    /// Every line's name, model, brand and unit come from the catalogue row the
    /// client named — never from the request. A line naming an item that does
    /// not exist is refused rather than written with blanks.
    ///
    /// The rate comes from the catalogue too, unless the caller holds boq.price
    /// and sent a different one. <paramref name="canPrice"/> is passed in rather
    /// than read here so this stays a pure builder, and so a caller cannot reach
    /// it without having decided the question.
    /// </summary>
    internal static async Task<(string? Error, List<BoqSection> Sections)> BuildAsync(
        Boq boq,
        IBoqWrite request,
        IApplicationDbContext context,
        TimeProvider timeProvider,
        bool canPrice,
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

                var catalogueRate = item.Rate ?? 0m;

                // Rounded before comparing. A UI that shows 57.00 can post
                // 57.000000001 back, and an unrounded comparison would read that
                // as a deliberate override and refuse a caller who changed
                // nothing.
                var requested = line.UnitRate.HasValue
                    ? BoqMath.Round(line.UnitRate.Value)
                    : (decimal?)null;

                var overridden = requested.HasValue && requested.Value != BoqMath.Round(catalogueRate);

                if (overridden && !canPrice)
                    return ($"You do not have permission to change the rate on \"{item.ItemName}\". "
                            + "Ask an administrator for the rate override grant, or leave the catalogue price.", []);

                var effectiveRate = overridden ? requested!.Value : catalogueRate;

                section.Lines.Add(new BoqLine
                {
                    Id = Guid.CreateVersion7(),
                    BoqSectionId = section.Id,
                    CameraId = item.Id,
                    ItemName = item.ItemName,
                    ModelNo = string.IsNullOrWhiteSpace(item.ModelNo) ? null : item.ModelNo,
                    Brand = item.Brand,
                    Type = string.IsNullOrWhiteSpace(item.Type) ? null : item.Type,
                    Uom = item.Uom,
                    Quantity = line.Quantity,
                    // Frozen with the rest of the line, so storage sized from this
                    // bill gives the same answer after the stock item is edited or
                    // retired. Copied as-is: a blank profile is recorded as blank
                    // rather than guessed at, because the guess would then be
                    // indistinguishable from a figure someone actually chose.
                    Resolution = item.Resolution,
                    BitrateMbps = item.BitrateMbps,
                    // Both recorded: the list price the catalogue held, and the
                    // price this line actually goes out at. They match unless
                    // somebody with the grant chose otherwise, and keeping both
                    // is what makes a discount reviewable afterwards.
                    CatalogueRate = catalogueRate,
                    UnitRate = effectiveRate,
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
