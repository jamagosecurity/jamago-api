using System.Security.Cryptography;
using FluentValidation;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.TravelClaims.Commands.SubmitTravelClaim;

/// <summary>
/// A staff member claims back an Uber ride they paid. They give only Uber's PDF receipt, the route, who came along and —
/// on a site trip — the site and the lunch. The day, time, amount and places are read from the PDF here, on the server,
/// so nobody can type a different figure; a receipt that cannot be read is refused. Lunch pays the bill, at most the
/// lunch rate for each person who ate. The claim waits for the accountant's approval.
/// </summary>
public sealed record SubmitTravelClaimCommand : IRequest<ApiResult<TravelClaimDto>>
{
    public TravelTripType? TripType { get; init; }
    public string? SiteName { get; init; }
    public IReadOnlyList<Guid> Companions { get; init; } = [];
    /// <summary>Who ate the site lunch: the claimant and/or companions. Empty means no lunch.</summary>
    public IReadOnlyList<Guid> AteLunch { get; init; } = [];
    public decimal LunchBillAmount { get; init; }
    public string? Note { get; init; }

    public string? ReceiptFileName { get; init; }
    public string? ReceiptContentType { get; init; }
    public long ReceiptSize { get; init; }
    public Stream? Receipt { get; init; }

    public string? LunchBillFileName { get; init; }
    public string? LunchBillContentType { get; init; }
    public long LunchBillSize { get; init; }
    public Stream? LunchBill { get; init; }

    internal bool HasLunch => AteLunch.Count > 0 || LunchBillAmount > 0 || LunchBillSize > 0;
}

public sealed class SubmitTravelClaimCommandValidator : AbstractValidator<SubmitTravelClaimCommand>
{
    public SubmitTravelClaimCommandValidator()
    {
        RuleFor(x => x.TripType).NotNull().WithMessage("Choose the route: office to accommodation, accommodation to office, or site.");
        RuleFor(x => x.SiteName).MaximumLength(200).WithMessage("The site name must be 200 characters or fewer.");
        RuleFor(x => x.SiteName).NotEmpty().When(x => x.TripType == TravelTripType.Site).WithMessage("Write the site name.");
        RuleFor(x => x.ReceiptFileName).Must(n => Path.GetExtension(n ?? "").Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Upload the Uber receipt as a PDF — the one Uber gives you, not a photo or screenshot.");
        RuleFor(x => x.Note).MaximumLength(500).WithMessage("The note must be 500 characters or fewer.");
        RuleFor(x => x.Companions.Count).LessThanOrEqualTo(20).WithMessage("Pick at most 20 people.");
        When(x => x.HasLunch, () =>
        {
            RuleFor(x => x.TripType).Equal(TravelTripType.Site).WithMessage("Lunch is only paid on site trips.");
            RuleFor(x => x.AteLunch).NotEmpty().WithMessage("Tick who ate lunch.");
            RuleFor(x => x.LunchBillAmount).Must(TravelClaimRules.IsMoney).WithMessage("Enter the lunch bill amount, like 36.00.");
        });
    }
}

public sealed class SubmitTravelClaimCommandHandler(
    IApplicationDbContext context, IFileStorage storage, IPdfTextReader pdfReader, ICurrentUser actor, TimeProvider clock)
    : IRequestHandler<SubmitTravelClaimCommand, ApiResult<TravelClaimDto>>
{
    public async Task<ApiResult<TravelClaimDto>> Handle(SubmitTravelClaimCommand r, CancellationToken ct)
    {
        static ApiResult<TravelClaimDto> Fail(string m) => ApiResult<TravelClaimDto>.Failure(m);
        if (!TravelClaimRules.IsStaff(actor)) return Fail("Only staff can claim Uber bills.");

        // The receipt first: everything about the ride comes from it.
        var (receipt, receiptError) = await TravelClaimRules.ReadFileAsync(r.Receipt, r.ReceiptFileName, r.ReceiptSize, "Uber receipt", ct);
        if (receipt is null) return Fail(receiptError!);
        var ride = UberReceiptParser.Parse(pdfReader.ReadText(receipt));
        if (!ride.Complete)
            return Fail("We could not read the date and amount from this receipt. Upload the PDF receipt from Uber (open the trip, then Download receipt), not a photo or screenshot.");
        if (!TravelClaimRules.IsMoney(ride.Amount!.Value)) return Fail("The amount on this receipt does not look right. Check it is an Uber trip receipt.");

        var today = TravelClaimRules.Today(clock);
        var day = ride.TripOn!.Value;
        if (day > today) return Fail("The receipt is dated in the future — check it is the right receipt.");
        if (day < today.AddDays(-TravelClaimRules.MaxTripAgeDays)) return Fail($"This trip is more than {TravelClaimRules.MaxTripAgeDays} days old and can no longer be claimed.");

        // Who came along: real, active staff, never the claimant.
        var me = actor.UserId;
        var companionIds = r.Companions.Distinct().Where(id => id != me).ToList();
        var companions = await context.AdminUsers.AsNoTracking()
            .Where(u => companionIds.Contains(u.Id) && u.IsActive && u.Role != Roles.Client)
            .Select(u => new { u.Id, u.FullName }).ToListAsync(ct);
        if (companions.Count != companionIds.Count) return Fail("One of the people you picked was not found.");

        var ate = r.AteLunch.Distinct().ToList();
        decimal rate = 0, lunchPay = 0;
        if (r.HasLunch)
        {
            if (ate.Any(id => id != me && !companionIds.Contains(id))) return Fail("Only you and the people who came with you can have lunch on this claim.");
            rate = await TravelClaimRules.LunchRateAsync(context, day, ct);
            lunchPay = TravelClaimRules.LunchPay(r.LunchBillAmount, ate.Count, rate);

            // One lunch per person per day, whichever claim it is on.
            var lunches = await context.TravelClaims.AsNoTracking()
                .Where(c => c.TripOn == day && TravelClaimRules.Live.Contains(c.Status) && c.Lines.Any(l => l.Kind == TravelClaimLineKind.Lunch))
                .Select(c => new { c.ClaimNumber, c.ClaimantId, c.ClaimantAteLunch, Ate = c.People.Where(p => p.AteLunch).Select(p => p.PersonId).ToList() })
                .ToListAsync(ct);
            foreach (var id in ate)
            {
                var clash = lunches.FirstOrDefault(c => (c.ClaimantId == id && c.ClaimantAteLunch) || c.Ate.Contains(id));
                if (clash is not null)
                    return Fail($"{(id == me ? "You" : companions.First(c => c.Id == id).FullName)} already had lunch paid on {day:d MMM} ({clash.ClaimNumber}).");
            }
        }

        // The lunch bill, and never a receipt that was claimed before.
        byte[]? lunchBill = null;
        if (r.HasLunch)
        {
            (lunchBill, var lunchError) = await TravelClaimRules.ReadFileAsync(r.LunchBill, r.LunchBillFileName, r.LunchBillSize, "lunch bill", ct);
            if (lunchBill is null) return Fail(lunchError!);
        }
        var hashes = new[] { receipt, lunchBill }.Where(b => b is not null).Select(b => Convert.ToHexString(SHA256.HashData(b!)).ToLowerInvariant()).ToList();
        if (hashes.Count == 2 && hashes[0] == hashes[1]) return Fail("The lunch bill and the Uber receipt are the same file.");
        var reused = await context.TravelClaimLines.AsNoTracking()
            .Where(l => hashes.Contains(l.FileHash) && TravelClaimRules.Live.Contains(l.TravelClaim.Status))
            .Select(l => new { l.Kind, l.TravelClaim.ClaimNumber }).FirstOrDefaultAsync(ct);
        if (reused is not null) return Fail($"This {(reused.Kind == TravelClaimLineKind.Ride ? "Uber receipt" : "lunch bill")} was already claimed ({reused.ClaimNumber}).");

        var now = clock.GetUtcNow().UtcDateTime;
        var claim = new TravelClaim
        {
            Id = Guid.CreateVersion7(),
            ClaimNumber = await TravelClaimRules.NextClaimNumberAsync(context, ct),
            ClaimantId = me,
            ClaimantName = actor.DisplayName ?? "Staff",
            TripOn = day,
            TripTime = ride.TripTime,
            TripType = r.TripType!.Value,
            SiteName = r.TripType == TravelTripType.Site ? Clean(r.SiteName) : null,
            Pickup = ride.Pickup,
            Dropoff = ride.Dropoff,
            ClaimantAteLunch = r.HasLunch && ate.Contains(me),
            Total = ride.Amount.Value + lunchPay,
            Note = Clean(r.Note),
            Status = TravelClaimStatus.Submitted,
            SubmittedAt = now,
            CreatedAt = now,
            People = companions.Select(c => new TravelClaimPerson
            {
                Id = Guid.CreateVersion7(), PersonId = c.Id, PersonName = c.FullName, AteLunch = r.HasLunch && ate.Contains(c.Id), CreatedAt = now,
            }).ToList(),
        };

        var rideLine = await Line(claim, TravelClaimLineKind.Ride, receipt, hashes[0], r.ReceiptFileName!, r.ReceiptContentType, ride.Amount.Value, ride.Amount.Value, 1, null, ct);
        rideLine.ReadFromReceipt = true;
        claim.Lines.Add(rideLine);
        if (lunchBill is not null)
            claim.Lines.Add(await Line(claim, TravelClaimLineKind.Lunch, lunchBill, hashes[1], r.LunchBillFileName!, r.LunchBillContentType, r.LunchBillAmount, lunchPay, ate.Count, rate, ct));
        claim.Events.Add(TravelClaimRules.Event(claim, TravelClaimAction.Submitted, actor, now, amount: claim.Total));

        context.TravelClaims.Add(claim);
        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch
        {
            // Nothing was recorded, so the files just stored would belong to no claim: take them away again.
            foreach (var line in claim.Lines) await storage.DeleteAsync(line.StorageKey, CancellationToken.None);
            throw;
        }
        return ApiResult<TravelClaimDto>.Success(TravelClaimRules.ToDto(claim, now));
    }

    private async Task<TravelClaimLine> Line(TravelClaim claim, TravelClaimLineKind kind, byte[] file, string hash, string fileName, string? contentType,
        decimal bill, decimal amount, int persons, decimal? rate, CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        var key = $"travel-claims/{claim.Id}/{kind.ToString().ToLowerInvariant()}-{id}{Path.GetExtension(fileName).ToLowerInvariant()}";
        await storage.SaveAsync(new MemoryStream(file), key, ct);
        return new TravelClaimLine
        {
            Id = id, TravelClaimId = claim.Id, Kind = kind, BillAmount = bill, Amount = amount, Persons = persons, RatePerPerson = rate,
            FileName = Path.GetFileName(fileName), StorageKey = key,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            FileHash = hash, CreatedAt = claim.CreatedAt,
        };
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
