using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.TravelClaims.Queries.GetTravelClaims;

/// <summary>
/// The accountant's board: every claim still open (waiting for approval or to be paid back), plus those paid, rejected
/// or withdrawn between two days (the last 30 by default), the payouts made in that time, and every person's balance.
/// With a person, that person's whole history instead.
/// </summary>
public sealed record GetTravelClaimsQuery(Guid? PersonId, DateOnly? From, DateOnly? To) : IRequest<ApiResult<TravelClaimsBoardDto>>;

public sealed class GetTravelClaimsQueryHandler(IApplicationDbContext context, TimeProvider clock)
    : IRequestHandler<GetTravelClaimsQuery, ApiResult<TravelClaimsBoardDto>>
{
    public async Task<ApiResult<TravelClaimsBoardDto>> Handle(GetTravelClaimsQuery r, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var today = TravelClaimRules.Today(clock);
        var to = r.To ?? today;
        var from = r.From ?? to.AddDays(-29);
        if (to < from) (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber > 366) return ApiResult<TravelClaimsBoardDto>.Failure("Pick at most one year.");
        var start = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue).AddHours(-3), DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-3), DateTimeKind.Utc);

        var all = await context.TravelClaims.AsNoTracking().WithDetails().ToListAsync(ct);
        bool Closed(DateTime? at) => at >= start && at < end;
        var shown = r.PersonId is { } person
            ? all.Where(c => c.ClaimantId == person)
            : all.Where(c => c.Status is TravelClaimStatus.Submitted or TravelClaimStatus.Approved || Closed(TravelClaimRules.ClosedAt(c)));

        var payouts = await context.Reimbursements.AsNoTracking().Include(p => p.Claims)
            .Where(p => r.PersonId != null ? p.PayeeId == r.PersonId : p.PaidAt >= start && p.PaidAt < end)
            .OrderByDescending(p => p.PaidAt).ToListAsync(ct);

        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var rate = await TravelClaimRules.LunchRateAsync(context, today, ct);
        return ApiResult<TravelClaimsBoardDto>.Success(new(
            rate,
            all.Where(c => c.Status == TravelClaimStatus.Submitted).Sum(c => c.Total), all.Count(c => c.Status == TravelClaimStatus.Submitted),
            all.Where(c => c.Status == TravelClaimStatus.Approved).Sum(TravelClaimRules.Payable), all.Count(c => c.Status == TravelClaimStatus.Approved),
            all.Where(c => c.Status == TravelClaimStatus.Paid && c.Reimbursement is not null && TravelClaimRules.DohaDay(c.Reimbursement.PaidAt) >= monthStart).Sum(TravelClaimRules.Payable),
            TravelClaimRules.Balances(all, now),
            shown.OrderByDescending(c => c.SubmittedAt).Select(c => TravelClaimRules.ToDto(c, now)).ToList(),
            payouts.Select(TravelClaimRules.ToDto).ToList()));
    }
}
