using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.TravelClaims.Commands.RejectTravelClaim;

/// <summary>The accountant turns a claim down — before it is paid back — with a reason the claimant sees.</summary>
public sealed record RejectTravelClaimCommand(Guid Id, string? Reason) : IRequest<ApiResult<TravelClaimDto>>;

public sealed class RejectTravelClaimCommandValidator : AbstractValidator<RejectTravelClaimCommand>
{
    public RejectTravelClaimCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Write why the claim is rejected — the person will see it.")
            .MaximumLength(500).WithMessage("The reason must be 500 characters or fewer.");
    }
}

public sealed class RejectTravelClaimCommandHandler(IApplicationDbContext context, ICurrentUser actor, TimeProvider clock)
    : IRequestHandler<RejectTravelClaimCommand, ApiResult<TravelClaimDto>>
{
    public async Task<ApiResult<TravelClaimDto>> Handle(RejectTravelClaimCommand r, CancellationToken ct)
    {
        var claim = await context.TravelClaims.WithDetails().FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (claim is null) return ApiResult<TravelClaimDto>.Failure("Claim not found.");
        if (claim.ClaimantId == actor.UserId) return ApiResult<TravelClaimDto>.Failure("You cannot decide on your own claim.");
        if (claim.Status is not (TravelClaimStatus.Submitted or TravelClaimStatus.Approved))
            return ApiResult<TravelClaimDto>.Failure("Only a claim that is waiting or approved and not yet paid back can be rejected.");

        var now = clock.GetUtcNow().UtcDateTime;
        var reason = r.Reason!.Trim();
        claim.Status = TravelClaimStatus.Rejected;
        claim.RejectedAt = now;
        claim.RejectedById = actor.UserId;
        claim.RejectedByName = actor.DisplayName ?? "Accountant";
        claim.RejectReason = reason;
        claim.UpdatedAt = now;
        context.TravelClaimEvents.Add(TravelClaimRules.Event(claim, TravelClaimAction.Rejected, actor, now, note: reason));
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ApiResult<TravelClaimDto>.Failure(TravelClaimRules.ChangedMeanwhile); }
        return ApiResult<TravelClaimDto>.Success(TravelClaimRules.ToDto(claim, now));
    }
}
