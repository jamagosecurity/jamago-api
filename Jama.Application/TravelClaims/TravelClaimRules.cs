using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.TravelClaims;

/// <summary>Rules shared by the Uber bill steps: the lunch rate, money and day arithmetic, numbering, files, and the shapes
/// sent to the screens.</summary>
internal static class TravelClaimRules
{
    public const decimal DefaultLunchRate = 12m;
    public const decimal MaxAmount = 99_999.99m;
    public const int MaxTripAgeDays = 90;
    internal static readonly string[] FileTypes = [".pdf", ".jpg", ".jpeg", ".png", ".webp", ".heic"];
    internal const long MaxFileBytes = 10 * 1024 * 1024;

    /// <summary>Claims that still count — for duplicate receipts and the one-lunch-a-day rule.</summary>
    public static readonly TravelClaimStatus[] Live = [TravelClaimStatus.Submitted, TravelClaimStatus.Approved, TravelClaimStatus.Paid];

    public static DateOnly DohaDay(DateTime utc) => DateOnly.FromDateTime(utc.AddHours(3));
    public static DateOnly Today(TimeProvider clock) => DohaDay(clock.GetUtcNow().UtcDateTime);
    public static bool IsMoney(decimal value) => value > 0 && value <= MaxAmount && decimal.Round(value, 2) == value;

    /// <summary>"Sara &lt;sara@jamago.qa&gt;" (how names of the signed-in person are kept) to "Sara".</summary>
    public static string Person(string? name)
    {
        var text = (name ?? "").Trim();
        var open = text.LastIndexOf('<');
        return (open > 0 && text.EndsWith('>') ? text[..open] : text).Trim();
    }

    public static bool IsStaff(ICurrentUser actor) => actor.Role != Roles.Client;

    /// <summary>The lunch rate for trips on a day: the latest that started on or before it; 12 QAR if none was ever set.</summary>
    public static async Task<decimal> LunchRateAsync(IApplicationDbContext context, DateOnly day, CancellationToken ct) =>
        await context.LunchRates.AsNoTracking().Where(r => r.EffectiveFrom <= day)
            .OrderByDescending(r => r.EffectiveFrom).ThenByDescending(r => r.CreatedAt)
            .Select(r => (decimal?)r.AmountPerPerson).FirstOrDefaultAsync(ct) ?? DefaultLunchRate;

    /// <summary>The lunch bill, but never more than the rate for each person who ate.</summary>
    public static decimal LunchPay(decimal bill, int people, decimal rate) => Math.Min(bill, rate * people);

    public static DateTime? ClosedAt(TravelClaim c) => c.Status switch
    {
        TravelClaimStatus.Paid => c.Reimbursement?.PaidAt,
        TravelClaimStatus.Rejected => c.RejectedAt,
        TravelClaimStatus.Withdrawn => c.WithdrawnAt,
        _ => null,
    };

    /// <summary>What a claim is worth to the claimant: the approved amount once approved, otherwise what was claimed.</summary>
    public static decimal Payable(TravelClaim c) => c.ApprovedAmount ?? c.Total;

    public static int DaysWaiting(TravelClaim c, DateTime now) =>
        DohaDay(ClosedAt(c) ?? now).DayNumber - DohaDay(c.SubmittedAt).DayNumber;

    public static IQueryable<TravelClaim> WithDetails(this IQueryable<TravelClaim> claims) => claims
        .Include(c => c.Lines).Include(c => c.People).Include(c => c.Events).Include(c => c.Reimbursement);

    public static TravelClaimDto ToDto(TravelClaim c, DateTime now) => new(
        c.Id, c.ClaimNumber, c.ClaimantId, c.ClaimantName, c.TripOn, c.TripTime, c.TripType.ToString(), c.SiteName, c.Pickup, c.Dropoff,
        c.ClaimantAteLunch, c.Total, c.ApprovedAmount, c.AdjustmentReason, Payable(c), c.Note, c.Status.ToString(),
        c.SubmittedAt, c.ApprovedAt, c.ApprovedByName, c.RejectedAt, c.RejectedByName, c.RejectReason, c.WithdrawnAt,
        c.Reimbursement?.PaidAt, c.Reimbursement?.PaidByName, c.Reimbursement?.Method.ToString(), c.Reimbursement?.Number,
        DaysWaiting(c, now),
        c.Lines.OrderBy(l => l.Kind).Select(l => new TravelClaimLineDto(l.Id, l.Kind.ToString(), l.BillAmount, l.Amount, l.Persons, l.RatePerPerson, l.ReadFromReceipt, l.FileName)).ToList(),
        c.People.OrderBy(p => p.PersonName).Select(p => new TravelClaimPersonDto(p.PersonId, p.PersonName, p.AteLunch)).ToList(),
        c.Events.OrderBy(e => e.CreatedAt).Select(e => new TravelClaimEventDto(e.Action.ToString(), e.ActorName, e.CreatedAt, e.Note, e.Amount)).ToList());

    public static ReimbursementDto ToDto(Reimbursement r) => new(
        r.Id, r.Number, r.PayeeId, r.PayeeName, r.Amount, r.Method.ToString(), r.Reference, r.PaidAt, r.PaidByName,
        r.Claims.OrderBy(c => c.ClaimNumber).Select(c => c.ClaimNumber).ToList());

    public static List<TravelBalanceDto> Balances(IEnumerable<TravelClaim> claims, DateTime now)
    {
        var today = DohaDay(now);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        return claims.GroupBy(c => c.ClaimantId).Select(g =>
        {
            var open = g.Where(c => c.Status is TravelClaimStatus.Submitted or TravelClaimStatus.Approved).ToList();
            var paid = g.Where(c => c.Status == TravelClaimStatus.Paid && c.Reimbursement is not null).ToList();
            return new TravelBalanceDto(
                g.Key, Person(g.OrderByDescending(c => c.SubmittedAt).First().ClaimantName),
                g.Where(c => c.Status == TravelClaimStatus.Submitted).Sum(c => c.Total), g.Count(c => c.Status == TravelClaimStatus.Submitted),
                g.Where(c => c.Status == TravelClaimStatus.Approved).Sum(Payable), g.Count(c => c.Status == TravelClaimStatus.Approved),
                paid.Where(c => DohaDay(c.Reimbursement!.PaidAt) >= monthStart).Sum(Payable), paid.Sum(Payable),
                paid.Select(c => (DateTime?)c.Reimbursement!.PaidAt).Max(),
                open.Count == 0 ? 0 : open.Max(c => DaysWaiting(c, now)));
        }).OrderByDescending(b => b.Owed + b.WaitingApproval).ThenBy(b => b.Name).ToList();
    }

    /// <summary>"UB-00001", from a database sequence: two claims submitted at the same moment never share a number.</summary>
    public static async Task<string> NextClaimNumberAsync(IApplicationDbContext context, CancellationToken ct) =>
        $"UB-{await context.NextSequenceValueAsync(DocumentSequences.TravelClaimNumbers, ct):D5}";

    /// <summary>"RB-00001", from a database sequence.</summary>
    public static async Task<string> NextReimbursementNumberAsync(IApplicationDbContext context, CancellationToken ct) =>
        $"RB-{await context.NextSequenceValueAsync(DocumentSequences.ReimbursementNumbers, ct):D5}";

    /// <summary>Checks an upload is the PDF or photo it says it is and reads it whole (at most 10 MB).</summary>
    public static async Task<(byte[]? Bytes, string? Error)> ReadFileAsync(Stream? stream, string? fileName, long size, string what, CancellationToken ct)
    {
        if (stream is null || size <= 0) return (null, $"Upload the {what}.");
        if (size > MaxFileBytes) return (null, $"The {what} is too big. The limit is 10 MB.");
        var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (!FileTypes.Contains(ext)) return (null, $"The {what} must be a PDF or a photo (JPG, PNG, WEBP, HEIC).");
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        if (!FileSignatures.Matches(ext, bytes.AsSpan(0, Math.Min(bytes.Length, FileSignatures.HeaderLength))))
            return (null, $"The {what} does not open as a PDF or photo. Check it and try again.");
        return (bytes, null);
    }

    public static TravelClaimEvent Event(TravelClaim claim, TravelClaimAction action, ICurrentUser actor, DateTime at, string? note = null, decimal? amount = null) => new()
    {
        Id = Guid.CreateVersion7(), TravelClaimId = claim.Id, Action = action, ActorId = actor.UserId,
        ActorName = actor.DisplayName ?? "Staff", Note = note, Amount = amount, CreatedAt = at,
    };

    public const string ChangedMeanwhile = "Someone else changed this claim just now. Refresh and try again.";
}
