using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.TravelClaims.Commands.ReimburseTravelClaims;

/// <summary>
/// The accountant gives money back to one person for one or more of their approved claims, in one payout: when, how
/// (cash or bank transfer) and how much is recorded once, and every claim it settles points to it.
/// </summary>
public sealed record ReimburseTravelClaimsCommand(IReadOnlyList<Guid> ClaimIds, ReimbursementMethod? Method, string? Reference)
    : IRequest<ApiResult<ReimbursementDto>>;

public sealed class ReimburseTravelClaimsCommandValidator : AbstractValidator<ReimburseTravelClaimsCommand>
{
    public ReimburseTravelClaimsCommandValidator()
    {
        RuleFor(x => x.ClaimIds).NotEmpty().WithMessage("Choose which claims are paid back.");
        RuleFor(x => x.Method).NotNull().WithMessage("Choose how it was paid back: cash or bank transfer.");
        RuleFor(x => x.Reference).MaximumLength(300).WithMessage("The reference must be 300 characters or fewer.");
    }
}

public sealed class ReimburseTravelClaimsCommandHandler(IApplicationDbContext context, ICurrentUser actor, TimeProvider clock)
    : IRequestHandler<ReimburseTravelClaimsCommand, ApiResult<ReimbursementDto>>
{
    public async Task<ApiResult<ReimbursementDto>> Handle(ReimburseTravelClaimsCommand r, CancellationToken ct)
    {
        static ApiResult<ReimbursementDto> Fail(string m) => ApiResult<ReimbursementDto>.Failure(m);
        var ids = r.ClaimIds.Distinct().ToList();
        var claims = await context.TravelClaims.Include(c => c.Events).Where(c => ids.Contains(c.Id)).ToListAsync(ct);
        if (claims.Count != ids.Count) return Fail("A claim was not found. Refresh the page.");
        if (claims.Select(c => c.ClaimantId).Distinct().Count() > 1) return Fail("Pay back one person at a time.");
        if (claims[0].ClaimantId == actor.UserId) return Fail("You cannot pay back your own claims. Another accountant or an administrator has to.");
        var notReady = claims.FirstOrDefault(c => c.Status != TravelClaimStatus.Approved);
        if (notReady is not null) return Fail($"{notReady.ClaimNumber} is not approved and waiting to be paid back.");

        var now = clock.GetUtcNow().UtcDateTime;
        var payout = new Reimbursement
        {
            Id = Guid.CreateVersion7(),
            Number = await TravelClaimRules.NextReimbursementNumberAsync(context, ct),
            PayeeId = claims[0].ClaimantId,
            PayeeName = TravelClaimRules.Person(claims[0].ClaimantName),
            Amount = claims.Sum(TravelClaimRules.Payable),
            Method = r.Method!.Value,
            Reference = string.IsNullOrWhiteSpace(r.Reference) ? null : r.Reference.Trim(),
            PaidAt = now,
            PaidById = actor.UserId,
            PaidByName = actor.DisplayName ?? "Accountant",
            CreatedAt = now,
        };
        context.Reimbursements.Add(payout);
        var how = payout.Method == ReimbursementMethod.Cash ? "cash" : "bank transfer";
        foreach (var claim in claims)
        {
            claim.Status = TravelClaimStatus.Paid;
            claim.ReimbursementId = payout.Id;
            claim.UpdatedAt = now;
            context.TravelClaimEvents.Add(TravelClaimRules.Event(claim, TravelClaimAction.Reimbursed, actor, now, $"{payout.Number} · {how}", TravelClaimRules.Payable(claim)));
        }
        payout.Claims = claims;
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Fail(TravelClaimRules.ChangedMeanwhile); }
        return ApiResult<ReimbursementDto>.Success(TravelClaimRules.ToDto(payout));
    }
}
