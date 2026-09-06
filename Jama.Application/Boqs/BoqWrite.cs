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
    /// <summary>
    /// The line's own id, when it is already on this BOQ.
    ///
    /// Only load-bearing for a line whose stock item has since been deleted:
    /// <see cref="CameraId"/> went null with it, so the id is the only way left
    /// to say which line is being kept. The content is still read from the stored
    /// row and never from the request — the client may say "keep this line", not
    /// what the line says.
    /// </summary>
    public Guid? Id { get; init; }

    /// <summary>
    /// The catalogue item this line prices. Null only for a line left behind by a
    /// deleted stock item, which is carried forward by <see cref="Id"/> instead.
    /// </summary>
    public Guid? CameraId { get; init; }

    public decimal Quantity { get; init; }

    /// <summary>
    /// A rate to use instead of the catalogue's. NULL means "price it from the
    /// catalogue", which is what an untouched line sends.
    ///
    /// Null rather than "send the catalogue rate back" on purpose: echoing
    /// today's price into every line would pin it there, so re-saving an
    /// untouched quotation after an admin corrected the catalogue would keep the
    /// stale figure and make it look like somebody had chosen it.
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

    /// <summary>A lump sum off the finished quotation, in QAR. Zero when none was
    /// agreed, which is the ordinary case.</summary>
    decimal SpecialDiscount { get; }

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
    internal const decimal DiscountMax = 9_999_999.99m;

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

        // Only the bounds here. Whether the discount fits inside the quotation is
        // settled by the writer, which is where the line rates are known — they
        // come from the catalogue, not from the request, so the total cannot be
        // worked out from what was posted.
        validator.RuleFor(x => x.SpecialDiscount)
            .GreaterThanOrEqualTo(0m).WithMessage("Discount cannot be negative.")
            .LessThanOrEqualTo(DiscountMax)
            .WithMessage($"Discount must be {DiscountMax:N2} or less.");

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
                // One or the other: a line names a catalogue item, or it names
                // itself — the case where the item behind it has been deleted.
                line.RuleFor(x => x)
                    .Must(x => x.CameraId.HasValue || x.Id.HasValue)
                    .WithMessage("Every line must point at a stock item.");

                line.RuleFor(x => x.CameraId)
                    .NotEmpty().WithMessage("Every line must point at a stock item.")
                    .When(x => x.CameraId.HasValue);

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
    /// The rate defaults to the catalogue's and may be overridden per line. Both
    /// figures are kept, so a negotiated price never erases the list price it was
    /// negotiated down from.
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
            .Where(l => l.CameraId.HasValue)
            .Select(l => l.CameraId!.Value)
            .Distinct()
            .ToList();

        var catalogue = await context.Cameras
            .AsNoTracking()
            .Where(x => wanted.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        if (wanted.Any(id => !catalogue.ContainsKey(id)))
            return ("A line refers to a stock item that no longer exists. Remove it and try again.", []);

        // Lines whose stock item has been deleted: CameraId went null with it, so
        // there is nothing to price them from except the row already stored. They
        // are read back here — scoped to THIS BOQ, so an id copied from another
        // document cannot pull its line across — and carried forward unchanged.
        //
        // Without this the whole document became unsaveable the moment one of its
        // items was retired, which is precisely the case the copied name and rate
        // exist to survive.
        // Named by neither: the validator refuses this, and so does the writer —
        // there is nothing to build the line from either way, and a line built
        // from nothing would go out blank on a bill.
        if (request.Sections.SelectMany(s => s.Lines).Any(l => !l.CameraId.HasValue && !l.Id.HasValue))
            return ("Every line must point at a stock item.", []);

        var carried = request.Sections
            .SelectMany(s => s.Lines)
            .Where(l => !l.CameraId.HasValue)
            .Select(l => l.Id!.Value)
            .Distinct()
            .ToList();

        var existing = carried.Count == 0
            ? []
            : await context.BoqLines
                .AsNoTracking()
                .Where(l => carried.Contains(l.Id) && l.Section.BoqId == boq.Id)
                .ToDictionaryAsync(l => l.Id, cancellationToken);

        if (carried.Any(id => !existing.ContainsKey(id)))
            return ("A line is no longer on this BOQ. Reload the page and try again.", []);

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
                // A retired item's line is described by the row already stored;
                // everything else by the catalogue. Either way the description
                // comes from the server, never from the request.
                var previous = line.CameraId.HasValue ? null : existing[line.Id!.Value];
                var item = line.CameraId.HasValue ? catalogue[line.CameraId.Value] : null;

                var catalogueRate = item?.Rate ?? previous?.CatalogueRate ?? 0m;

                // Rounded on the way in: a UI showing 57.00 can post 57.000000001
                // back, and storing that would print one price and hold another.
                var effectiveRate = line.UnitRate.HasValue
                    ? BoqMath.Round(line.UnitRate.Value)
                    // A carried line falls back to the price it already went out
                    // at, not to a catalogue rate that no longer exists.
                    : previous?.UnitRate ?? catalogueRate;

                section.Lines.Add(new BoqLine
                {
                    Id = Guid.CreateVersion7(),
                    BoqSectionId = section.Id,
                    CameraId = item?.Id,
                    ItemName = item?.ItemName ?? previous!.ItemName,
                    ModelNo = Clean(item is null ? previous!.ModelNo : item.ModelNo),
                    Brand = item?.Brand ?? previous!.Brand,
                    Type = Clean(item is null ? previous!.Type : item.Type),
                    Uom = item?.Uom ?? previous!.Uom,
                    Quantity = line.Quantity,
                    // Frozen with the rest of the line, so storage sized from this
                    // bill gives the same answer after the stock item is edited or
                    // retired. Copied as-is: a blank profile is recorded as blank
                    // rather than guessed at, because the guess would then be
                    // indistinguishable from a figure someone actually chose.
                    Resolution = item?.Resolution ?? previous!.Resolution,
                    BitrateMbps = item is null ? previous!.BitrateMbps : item.BitrateMbps,
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
        boq.SpecialDiscount = BoqMath.Round(request.SpecialDiscount);

        // Refused, not clamped: someone typing 5,000 off a 500 quotation has
        // mistyped, and a quotation silently worth nothing is worse than a save
        // that comes back and says so. BoqMath still clamps on write, for the
        // case where the lines are cut down under a discount already agreed.
        if (boq.SpecialDiscount > boq.Total)
            return ($"A discount of {boq.SpecialDiscount:N2} QAR is more than the quotation total of {boq.Total:N2} QAR.", []);

        BoqMath.ApplyDiscount(boq);
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
