using FluentValidation;
using Jama.Domain.Entities;
using Jama.Domain.Enums;

namespace Jama.Application.Quotations;

public sealed record QuotationLineInput
{
    /// <summary>Soft link to the catalogue item this came from, when it came from
    /// one. Null for a free-typed line.</summary>
    public Guid? CameraId { get; init; }

    public string? ItemName { get; init; }
    public string? ModelNo { get; init; }
    public string? Brand { get; init; }
    public string? Description { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitRate { get; init; }
    public decimal DiscountPercent { get; init; }
    public decimal TaxPercent { get; init; }
}

/// <summary>
/// The writable shape of a quotation, implemented by the create and update
/// commands so neither can drift from the other's rules.
/// </summary>
public interface IQuotationWrite
{
    string? CustomerName { get; }
    string? CustomerCompany { get; }
    string? CustomerEmail { get; }
    string? CustomerPhone { get; }
    string? CustomerAddress { get; }
    DateOnly? IssueDate { get; }
    DateOnly? ValidUntil { get; }
    QuotationStatus Status { get; }
    string? Notes { get; }
    string? Terms { get; }
    IReadOnlyList<QuotationLineInput> Lines { get; }
}

internal static class QuotationWriteRules
{
    internal const int NameMaxLength = 200;
    internal const int EmailMaxLength = 256;
    internal const int PhoneMaxLength = 40;
    internal const int AddressMaxLength = 500;
    internal const int NotesMaxLength = 2000;
    internal const int ItemNameMaxLength = 200;

    /// <summary>A quote of a thousand lines is a runaway loop, not an offer.</summary>
    internal const int MaxLines = 200;

    internal const decimal MoneyMax = 9_999_999.99m;
    internal const decimal QuantityMax = 100_000m;

    internal static void Apply<T>(AbstractValidator<T> validator) where T : IQuotationWrite
    {
        validator.RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Customer name is required.")
            .MaximumLength(NameMaxLength)
            .WithMessage($"Customer name must be {NameMaxLength} characters or fewer.");

        validator.RuleFor(x => x.CustomerCompany).MaximumLength(NameMaxLength);
        validator.RuleFor(x => x.CustomerPhone).MaximumLength(PhoneMaxLength);
        validator.RuleFor(x => x.CustomerAddress).MaximumLength(AddressMaxLength);
        validator.RuleFor(x => x.Notes).MaximumLength(NotesMaxLength);
        validator.RuleFor(x => x.Terms).MaximumLength(NotesMaxLength);

        validator.RuleFor(x => x.CustomerEmail)
            .EmailAddress().WithMessage("Enter a valid email address.")
            .MaximumLength(EmailMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.CustomerEmail));

        validator.RuleFor(x => x.Status).IsInEnum().WithMessage("Select a valid status.");

        // A quote that expires before it is issued cannot be honoured.
        validator.RuleFor(x => x.ValidUntil)
            .Must((request, validUntil) => validUntil >= (request.IssueDate ?? DateOnly.MinValue))
            .WithMessage("Valid-until date cannot be before the issue date.")
            .When(x => x.ValidUntil.HasValue && x.IssueDate.HasValue);

        validator.RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("Add at least one line to the quotation.")
            .Must(lines => lines.Count <= MaxLines)
            .WithMessage($"A quotation cannot have more than {MaxLines} lines.");

        validator.RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(x => x.ItemName)
                .NotEmpty().WithMessage("Every line needs an item name.")
                .MaximumLength(ItemNameMaxLength);

            line.RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("Line quantity must be greater than 0.")
                .LessThanOrEqualTo(QuantityMax)
                .WithMessage($"Line quantity must be {QuantityMax:N0} or fewer.");

            line.RuleFor(x => x.UnitRate)
                .GreaterThanOrEqualTo(0).WithMessage("Line rate cannot be negative.")
                .LessThanOrEqualTo(MoneyMax)
                .WithMessage($"Line rate must be {MoneyMax:N2} or less.");

            line.RuleFor(x => x.DiscountPercent)
                .InclusiveBetween(0, 100).WithMessage("Line discount must be between 0 and 100%.");

            line.RuleFor(x => x.TaxPercent)
                .InclusiveBetween(0, 100).WithMessage("Line tax must be between 0 and 100%.");
        });
    }
}

internal static class QuotationWriter
{
    /// <summary>
    /// Copies a request onto the entity and rebuilds its lines from scratch.
    ///
    /// Lines are replaced wholesale rather than diffed: a quotation is edited as
    /// a whole document — rows reordered, removed, retyped — and matching them up
    /// by id would be a lot of machinery to preserve rows nobody is tracking.
    /// </summary>
    internal static void Apply(Quotation quotation, IQuotationWrite request, TimeProvider timeProvider)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        quotation.CustomerName = request.CustomerName?.Trim() ?? string.Empty;
        quotation.CustomerCompany = Clean(request.CustomerCompany);
        quotation.CustomerEmail = Clean(request.CustomerEmail);
        quotation.CustomerPhone = Clean(request.CustomerPhone);
        quotation.CustomerAddress = Clean(request.CustomerAddress);
        quotation.IssueDate = request.IssueDate ?? today;
        quotation.ValidUntil = request.ValidUntil;
        quotation.Status = request.Status;
        quotation.Notes = Clean(request.Notes);
        quotation.Terms = Clean(request.Terms);

        quotation.Lines.Clear();

        var order = 0;
        foreach (var input in request.Lines)
        {
            quotation.Lines.Add(new QuotationLine
            {
                Id = Guid.CreateVersion7(),
                QuotationId = quotation.Id,
                CameraId = input.CameraId,
                ItemName = input.ItemName?.Trim() ?? string.Empty,
                ModelNo = Clean(input.ModelNo),
                Brand = Clean(input.Brand),
                Description = Clean(input.Description),
                Quantity = input.Quantity,
                UnitRate = input.UnitRate,
                DiscountPercent = input.DiscountPercent,
                TaxPercent = input.TaxPercent,
                SortOrder = order++,
                CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            });
        }

        QuotationMath.Recalculate(quotation);
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
