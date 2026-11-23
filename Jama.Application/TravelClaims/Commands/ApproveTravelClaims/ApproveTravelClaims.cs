using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.TravelClaims.Commands.ApproveTravelClaims;

/// <summary>One claim to approve, at the amount claimed (null) or a lower amount with the reason for it.</summary>
public sealed record TravelClaimApproval(Guid ClaimId, decimal? ApprovedAmount, string? Reason);

/// <summary>
/// The accountant approves one or many claims at once. Each may be approved at a lower amount than claimed — never
/// higher, and never nothing (that is a rejection) — with a reason. Approved amounts are then owed to the claimant until
/// paid back. All or nothing: if one claim cannot be approved, none is, so the totals the accountant saw stay true.
/// Nobody approves their own claim.
/// </summary>
public sealed record ApproveTravelClaimsCommand(IReadOnlyList<TravelClaimApproval> Items) : IRequest<ApiResult<IReadOnlyList<TravelClaimDto>>>;

public sealed class ApproveTravelClaimsCommandValidator : AbstractValidator<ApproveTravelClaimsCommand>
{
    public ApproveTravelClaimsCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithMessage("Choose which bills to approve.")
            .Must(i => i.Count <= 200).WithMessage("Approve at most 200 bills at a time.")
            .Must(i => i.Select(x => x.ClaimId).Distinct().Count() == i.Count).WithMessage("A bill is listed twice.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ApprovedAmount)
                .Must(a => a is null || TravelClaimRules.IsMoney(a.Value))
                .WithMessage("Enter the approved amount, like 24.00. To pay nothing, reject the bill instead.");
            item.RuleFor(i => i.Reason).MaximumLength(300).WithMessage("The reason must be 300 characters or fewer.");
        });
    }
}

public sealed class ApproveTravelClaimsCommandHandler(IApplicationDbContext context, ICurrentUser actor, TimeProvider clock)
    : IRequestHandler<ApproveTravelClaimsCommand, ApiResult<IReadOnlyList<TravelClaimDto>>>
{
    public async Task<ApiResult<IReadOnlyList<TravelClaimDto>>> Handle(ApproveTravelClaimsCommand r, CancellationToken ct)
    {
        static ApiResult<IReadOnlyList<TravelClaimDto>> Fail(string m) => ApiResult<IReadOnlyList<TravelClaimDto>>.Failure(m);
        var ids = r.Items.Select(i => i.ClaimId).ToList();
        var claims = await context.TravelClaims.WithDetails().Where(c => ids.Contains(c.Id)).ToListAsync(ct);
        if (claims.Count != ids.Count) return Fail("A bill was not found. Refresh the page.");

        // Check every bill first; change nothing until all of them pass.
        var decisions = new List<(Domain.Entities.TravelClaim Claim, decimal Approved, string? Reason)>();
        foreach (var item in r.Items)
        {
            var claim = claims.Single(c => c.Id == item.ClaimId);
            if (claim.ClaimantId == actor.UserId)
                return Fail($"{claim.ClaimNumber} is your own claim — another accountant or an administrator has to approve it.");
            if (claim.Status != TravelClaimStatus.Submitted)
                return Fail($"{claim.ClaimNumber} is no longer waiting for approval. Refresh the page.");

            var approved = item.ApprovedAmount ?? claim.Total;
            if (approved > claim.Total)
                return Fail($"{claim.ClaimNumber}: the approved amount cannot be more than the {claim.Total:N2} QAR claimed.");
            var reason = string.IsNullOrWhiteSpace(item.Reason) ? null : item.Reason.Trim();
            if (approved < claim.Total && reason is null)
                return Fail($"{claim.ClaimNumber}: write why it is approved at {approved:N2} instead of {claim.Total:N2} QAR.");
            decisions.Add((claim, approved, approved < claim.Total ? reason : null));
        }

        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var (claim, approved, reason) in decisions)
        {
            claim.Status = TravelClaimStatus.Approved;
            claim.ApprovedAmount = approved;
            claim.AdjustmentReason = reason;
            claim.ApprovedAt = now;
            claim.ApprovedById = actor.UserId;
            claim.ApprovedByName = actor.DisplayName ?? "Accountant";
            claim.UpdatedAt = now;
            var note = reason is null ? null : $"Approved {approved:N2} of {claim.Total:N2} QAR claimed — {reason}";
            context.TravelClaimEvents.Add(TravelClaimRules.Event(claim, TravelClaimAction.Approved, actor, now, note, approved));
        }

        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Fail(TravelClaimRules.ChangedMeanwhile); }
        IReadOnlyList<TravelClaimDto> result = r.Items.Select(i => TravelClaimRules.ToDto(claims.Single(c => c.Id == i.ClaimId), now)).ToList();
        return ApiResult<IReadOnlyList<TravelClaimDto>>.Success(result);
    }
}
