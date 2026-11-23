using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.TravelClaims.Commands.WithdrawTravelClaim;

/// <summary>The claimant takes back a claim the accountant has not decided on yet. It stays on record as Withdrawn.</summary>
public sealed record WithdrawTravelClaimCommand(Guid Id) : IRequest<ApiResult<TravelClaimDto>>;

public sealed class WithdrawTravelClaimCommandHandler(IApplicationDbContext context, ICurrentUser actor, TimeProvider clock)
    : IRequestHandler<WithdrawTravelClaimCommand, ApiResult<TravelClaimDto>>
{
    public async Task<ApiResult<TravelClaimDto>> Handle(WithdrawTravelClaimCommand r, CancellationToken ct)
    {
        var claim = await context.TravelClaims.WithDetails().FirstOrDefaultAsync(c => c.Id == r.Id && c.ClaimantId == actor.UserId, ct);
        if (claim is null) return ApiResult<TravelClaimDto>.Failure("Claim not found.");
        if (claim.Status != TravelClaimStatus.Submitted)
            return ApiResult<TravelClaimDto>.Failure("The accountant has already decided on this claim, so it cannot be taken back.");

        var now = clock.GetUtcNow().UtcDateTime;
        claim.Status = TravelClaimStatus.Withdrawn;
        claim.WithdrawnAt = now;
        claim.UpdatedAt = now;
        context.TravelClaimEvents.Add(TravelClaimRules.Event(claim, TravelClaimAction.Withdrawn, actor, now));
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ApiResult<TravelClaimDto>.Failure(TravelClaimRules.ChangedMeanwhile); }
        return ApiResult<TravelClaimDto>.Success(TravelClaimRules.ToDto(claim, now));
    }
}
