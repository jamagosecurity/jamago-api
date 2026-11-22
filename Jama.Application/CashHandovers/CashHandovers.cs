using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.CashHandovers;

/// <summary>
/// The accountant's panel. Every day, people who collected cash on jobs hand it to the accountant: the panel lists, per
/// person, the cash recorded for them that has not been handed over yet, the accountant counts it and confirms. MOI and
/// Non-MOI are kept apart — each is its own list and its own handover. A handover is only a record of what was received:
/// it never changes a bill or a payment.
/// </summary>
public static class CashKinds
{
    public const string Moi = "MOI";
    public const string NonMoi = "NonMOI";

    /// <summary>"moi" / "nonmoi" (any case, "non-moi" too) to the stored name; null if it is neither.</summary>
    public static string? Parse(string? kind) => (kind ?? "").Trim().Replace("-", "").ToLowerInvariant() switch
    {
        "moi" => Moi,
        "nonmoi" => NonMoi,
        _ => null,
    };

    internal static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(3));
    internal static DateOnly DohaDay(DateTime utc) => DateOnly.FromDateTime(utc.AddHours(3));

    public const string Office = "Received at the office";
}

public sealed record HandoverPaymentDto(
    Guid PaymentId, Guid JobId, string JobNumber, string CustomerName, decimal Amount, DateOnly ReceivedOn,
    string RecordedByName, string? Note);

public sealed record PendingHandoverDto(
    Guid? CollectorId, string CollectorName, decimal Total, DateOnly OldestDay, DateOnly NewestDay,
    IReadOnlyList<HandoverPaymentDto> Payments);

public sealed record PendingHandoversDto(string Kind, DateOnly Today, decimal Total, IReadOnlyList<PendingHandoverDto> Groups);

public sealed record CashHandoverDto(
    Guid Id, string Kind, Guid? CollectorId, string CollectorName, decimal Expected, decimal Received, decimal Difference,
    string? Note, string ReceivedByName, DateTime ReceivedAt, IReadOnlyList<HandoverPaymentDto> Payments);

public sealed record OnlinePaymentDto(
    Guid PaymentId, Guid JobId, string JobNumber, string CustomerName, decimal Amount, DateOnly ReceivedOn,
    string RecordedByName, bool HasReceipt, string? Note);

public sealed record OnlinePaymentsDto(string Kind, DateOnly From, DateOnly To, decimal Total, IReadOnlyList<OnlinePaymentDto> Rows);

/// <summary>One cash payment, whichever kind of job it came from.</summary>
internal sealed record CashRow(
    Guid PaymentId, Guid JobId, string JobNumber, string CustomerName, decimal Amount, DateOnly ReceivedOn,
    Guid? CollectedById, string? CollectedByName, string RecordedByName, string? Note, Guid? HandoverId)
{
    public HandoverPaymentDto ToDto() => new(PaymentId, JobId, JobNumber, CustomerName, Amount, ReceivedOn, RecordedByName, Note);
}

internal static class CashLookup
{
    /// <summary>Every cash payment of a kind, with who took it and which handover (if any) received it.</summary>
    public static async Task<List<CashRow>> CashAsync(IApplicationDbContext context, string kind, bool pendingOnly, CancellationToken ct)
    {
        if (kind == CashKinds.Moi)
        {
            var q = context.MoiJobPayments.AsNoTracking().Where(p => p.Method == "Cash");
            if (pendingOnly) q = q.Where(p => p.CashHandoverId == null);
            return await q.OrderBy(p => p.ReceivedOn).ThenBy(p => p.CreatedAt)
                .Select(p => new CashRow(p.Id, p.MoiJobId, p.MoiJob.JobNumber, p.MoiJob.CustomerName, p.Amount, p.ReceivedOn,
                    p.CollectedById, p.CollectedByName, p.RecordedByName, p.Note, p.CashHandoverId))
                .ToListAsync(ct);
        }

        var n = context.NonMoiJobPayments.AsNoTracking().Where(p => p.Method == "Cash");
        if (pendingOnly) n = n.Where(p => p.CashHandoverId == null);
        return await n.OrderBy(p => p.ReceivedOn).ThenBy(p => p.CreatedAt)
            .Select(p => new CashRow(p.Id, p.NonMoiJobId, p.NonMoiJob.JobNumber, p.NonMoiJob.CustomerName, p.Amount, p.ReceivedOn,
                p.CollectedById, p.CollectedByName, p.RecordedByName, p.Note, p.CashHandoverId))
            .ToListAsync(ct);
    }
}

// ===== What is waiting to be handed over =====

/// <summary>Per person, the cash recorded for them on this kind of job that has not been handed over yet (any day, oldest first).</summary>
public sealed record GetPendingHandoversQuery(string Kind) : IRequest<ApiResult<PendingHandoversDto>>;

public sealed class GetPendingHandoversQueryHandler(IApplicationDbContext context, TimeProvider clock)
    : IRequestHandler<GetPendingHandoversQuery, ApiResult<PendingHandoversDto>>
{
    public async Task<ApiResult<PendingHandoversDto>> Handle(GetPendingHandoversQuery r, CancellationToken ct)
    {
        var kind = CashKinds.Parse(r.Kind);
        if (kind is null) return ApiResult<PendingHandoversDto>.Failure("Choose MOI or Non-MOI.");

        var rows = await CashLookup.CashAsync(context, kind, pendingOnly: true, ct);
        var groups = rows
            .GroupBy(x => x.CollectedById)
            .Select(g => new PendingHandoverDto(
                g.Key,
                g.Select(x => x.CollectedByName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? CashKinds.Office,
                g.Sum(x => x.Amount), g.Min(x => x.ReceivedOn), g.Max(x => x.ReceivedOn),
                g.Select(x => x.ToDto()).ToList()))
            .OrderBy(g => g.CollectorId is null)        // the office last
            .ThenBy(g => g.CollectorName)
            .ToList();
        return ApiResult<PendingHandoversDto>.Success(new(kind, CashKinds.Today(clock), groups.Sum(g => g.Total), groups));
    }
}

// ===== Receiving it =====

/// <summary>
/// The accountant counted the cash one person handed over and records it. <paramref name="ExpectedAmount"/> is what the
/// page showed: if cash was added or changed since, the handover is refused so nobody confirms a figure they did not see.
/// A different amount is allowed, but needs a short note saying why.
/// </summary>
public sealed record ConfirmCashHandoverCommand(string Kind, Guid? CollectorId, decimal ExpectedAmount, decimal ReceivedAmount, string? Note)
    : IRequest<ApiResult<CashHandoverDto>>;

public sealed class ConfirmCashHandoverCommandValidator : AbstractValidator<ConfirmCashHandoverCommand>
{
    public const decimal Max = 9_999_999.99m;

    public ConfirmCashHandoverCommandValidator()
    {
        RuleFor(x => x.Kind).Must(k => CashKinds.Parse(k) is not null).WithMessage("Choose MOI or Non-MOI.");
        RuleFor(x => x.ReceivedAmount)
            .GreaterThanOrEqualTo(0m).WithMessage("The amount received cannot be negative.")
            .LessThanOrEqualTo(Max).WithMessage($"The amount received must be {Max:N2} or less.")
            .Must(a => decimal.Round(a, 2) == a).WithMessage("The amount can have at most 2 decimals.");
        RuleFor(x => x.Note).MaximumLength(500).WithMessage("The note must be 500 characters or fewer.");
    }
}

public sealed class ConfirmCashHandoverCommandHandler(IApplicationDbContext context, ICurrentUser actor, TimeProvider clock)
    : IRequestHandler<ConfirmCashHandoverCommand, ApiResult<CashHandoverDto>>
{
    public async Task<ApiResult<CashHandoverDto>> Handle(ConfirmCashHandoverCommand r, CancellationToken ct)
    {
        static ApiResult<CashHandoverDto> Fail(string m) => ApiResult<CashHandoverDto>.Failure(m);

        var kind = CashKinds.Parse(r.Kind);
        if (kind is null) return Fail("Choose MOI or Non-MOI.");

        var rows = await CashLookup.CashAsync(context, kind, pendingOnly: true, ct);
        rows = rows.Where(x => x.CollectedById == r.CollectorId).ToList();
        if (rows.Count == 0) return Fail("Nothing is waiting from this person. Refresh the page.");

        var expected = rows.Sum(x => x.Amount);
        if (expected != r.ExpectedAmount)
            return Fail("Cash was added or changed since you opened this page. Refresh, then count again.");

        var note = string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim();
        if (r.ReceivedAmount != expected && note is null)
            return Fail(r.ReceivedAmount < expected
                ? $"QAR {expected - r.ReceivedAmount:N2} is missing. Write a short note saying why."
                : $"QAR {r.ReceivedAmount - expected:N2} is extra. Write a short note saying why.");

        var now = clock.GetUtcNow().UtcDateTime;
        var handover = new CashHandover
        {
            Id = Guid.CreateVersion7(),
            Kind = kind,
            CollectorId = r.CollectorId,
            CollectorName = rows.Select(x => x.CollectedByName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? CashKinds.Office,
            ExpectedAmount = expected,
            ReceivedAmount = r.ReceivedAmount,
            Note = note,
            ReceivedById = actor.UserId,
            ReceivedByName = actor.DisplayName ?? "Accountant",
            CreatedAt = now,
        };
        context.CashHandovers.Add(handover);

        var ids = rows.Select(x => x.PaymentId).ToList();
        if (kind == CashKinds.Moi)
            foreach (var p in await context.MoiJobPayments.Where(p => ids.Contains(p.Id)).ToListAsync(ct)) p.CashHandoverId = handover.Id;
        else
            foreach (var p in await context.NonMoiJobPayments.Where(p => ids.Contains(p.Id)).ToListAsync(ct)) p.CashHandoverId = handover.Id;

        await context.SaveChangesAsync(ct);
        return ApiResult<CashHandoverDto>.Success(ToDto(handover, rows));
    }

    internal static CashHandoverDto ToDto(CashHandover h, IEnumerable<CashRow> rows) => new(
        h.Id, h.Kind, h.CollectorId, h.CollectorName, h.ExpectedAmount, h.ReceivedAmount, h.ReceivedAmount - h.ExpectedAmount,
        h.Note, h.ReceivedByName, h.CreatedAt, rows.Select(x => x.ToDto()).ToList());
}

// ===== What was received =====

/// <summary>Handovers recorded between two days (Doha dates, both included); the last 7 days by default.</summary>
public sealed record GetCashHandoverHistoryQuery(string Kind, DateOnly? From, DateOnly? To) : IRequest<ApiResult<IReadOnlyList<CashHandoverDto>>>;

public sealed class GetCashHandoverHistoryQueryHandler(IApplicationDbContext context, TimeProvider clock)
    : IRequestHandler<GetCashHandoverHistoryQuery, ApiResult<IReadOnlyList<CashHandoverDto>>>
{
    public async Task<ApiResult<IReadOnlyList<CashHandoverDto>>> Handle(GetCashHandoverHistoryQuery r, CancellationToken ct)
    {
        var kind = CashKinds.Parse(r.Kind);
        if (kind is null) return ApiResult<IReadOnlyList<CashHandoverDto>>.Failure("Choose MOI or Non-MOI.");

        var today = CashKinds.Today(clock);
        var to = r.To ?? today;
        var from = r.From ?? to.AddDays(-6);
        if (to < from) (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber > 366) return ApiResult<IReadOnlyList<CashHandoverDto>>.Failure("Pick at most one year.");

        // 00:00 Doha of the first day up to 00:00 Doha after the last, as UTC times (PostgreSQL refuses a time with no kind).
        var start = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue).AddHours(-3), DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-3), DateTimeKind.Utc);
        var handovers = await context.CashHandovers.AsNoTracking()
            .Where(h => h.Kind == kind && h.CreatedAt >= start && h.CreatedAt < end)
            .OrderByDescending(h => h.CreatedAt).ToListAsync(ct);

        var ids = handovers.Select(h => h.Id).ToList();
        var rows = (await CashLookup.CashAsync(context, kind, pendingOnly: false, ct))
            .Where(x => x.HandoverId is not null && ids.Contains(x.HandoverId.Value)).ToList();

        IReadOnlyList<CashHandoverDto> result = handovers
            .Select(h => ConfirmCashHandoverCommandHandler.ToDto(h, rows.Where(x => x.HandoverId == h.Id)))
            .ToList();
        return ApiResult<IReadOnlyList<CashHandoverDto>>.Success(result);
    }
}

// ===== Online transfers, to check against the bank =====

/// <summary>Online (bank transfer) payments received between two days (Doha dates, both included); today by default.
/// Read-only: transfers go straight to the bank, so there is nothing to hand over.</summary>
public sealed record GetOnlinePaymentsQuery(string Kind, DateOnly? From, DateOnly? To) : IRequest<ApiResult<OnlinePaymentsDto>>;

public sealed class GetOnlinePaymentsQueryHandler(IApplicationDbContext context, TimeProvider clock)
    : IRequestHandler<GetOnlinePaymentsQuery, ApiResult<OnlinePaymentsDto>>
{
    public async Task<ApiResult<OnlinePaymentsDto>> Handle(GetOnlinePaymentsQuery r, CancellationToken ct)
    {
        var kind = CashKinds.Parse(r.Kind);
        if (kind is null) return ApiResult<OnlinePaymentsDto>.Failure("Choose MOI or Non-MOI.");

        var today = CashKinds.Today(clock);
        var from = r.From ?? today;
        var to = r.To ?? from;
        if (to < from) (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber > 366) return ApiResult<OnlinePaymentsDto>.Failure("Pick at most one year.");

        var rows = kind == CashKinds.Moi
            ? await context.MoiJobPayments.AsNoTracking()
                .Where(p => p.Method == "Online" && p.ReceivedOn >= from && p.ReceivedOn <= to)
                .OrderByDescending(p => p.ReceivedOn).ThenByDescending(p => p.CreatedAt)
                .Select(p => new OnlinePaymentDto(p.Id, p.MoiJobId, p.MoiJob.JobNumber, p.MoiJob.CustomerName, p.Amount, p.ReceivedOn,
                    p.RecordedByName, p.ReceiptStorageKey != null, p.Note))
                .ToListAsync(ct)
            : await context.NonMoiJobPayments.AsNoTracking()
                .Where(p => p.Method == "Online" && p.ReceivedOn >= from && p.ReceivedOn <= to)
                .OrderByDescending(p => p.ReceivedOn).ThenByDescending(p => p.CreatedAt)
                .Select(p => new OnlinePaymentDto(p.Id, p.NonMoiJobId, p.NonMoiJob.JobNumber, p.NonMoiJob.CustomerName, p.Amount, p.ReceivedOn,
                    p.RecordedByName, p.ReceiptStorageKey != null, p.Note))
                .ToListAsync(ct);
        return ApiResult<OnlinePaymentsDto>.Success(new(kind, from, to, rows.Sum(x => x.Amount), rows));
    }
}
