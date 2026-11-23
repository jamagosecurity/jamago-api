using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.TravelClaims.Queries.GetMyTravelClaims;

/// <summary>The signed-in person's own claims, newest first, with their balance: waiting, owed, and paid back.</summary>
public sealed record GetMyTravelClaimsQuery : IRequest<ApiResult<MyTravelClaimsDto>>;

public sealed class GetMyTravelClaimsQueryHandler(IApplicationDbContext context, ICurrentUser actor, TimeProvider clock)
    : IRequestHandler<GetMyTravelClaimsQuery, ApiResult<MyTravelClaimsDto>>
{
    public async Task<ApiResult<MyTravelClaimsDto>> Handle(GetMyTravelClaimsQuery r, CancellationToken ct)
    {
        if (!TravelClaimRules.IsStaff(actor)) return ApiResult<MyTravelClaimsDto>.Failure("Only staff can claim Uber bills.");
        var now = clock.GetUtcNow().UtcDateTime;
        var claims = await context.TravelClaims.AsNoTracking().WithDetails()
            .Where(c => c.ClaimantId == actor.UserId).OrderByDescending(c => c.SubmittedAt).ToListAsync(ct);
        var balance = TravelClaimRules.Balances(claims, now).FirstOrDefault()
            ?? new TravelBalanceDto(actor.UserId, TravelClaimRules.Person(actor.DisplayName), 0, 0, 0, 0, 0, 0, null, 0);
        var rate = await TravelClaimRules.LunchRateAsync(context, TravelClaimRules.Today(clock), ct);
        return ApiResult<MyTravelClaimsDto>.Success(new(balance, rate, claims.Select(c => TravelClaimRules.ToDto(c, now)).ToList()));
    }
}
