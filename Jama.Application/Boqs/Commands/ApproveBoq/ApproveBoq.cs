using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Boqs.Commands.ApproveBoq;

/// <summary>
/// Approves a quotation that was submitted for approval.
///
/// Gated on boq.approve at the endpoint — the grant is the whole point of the
/// separation, and checking it here as well would put the same rule in two
/// places to drift apart.
/// </summary>
public sealed record ApproveBoqCommand : IRequest<ApiResult<BoqDto>>
{
    /// <summary>Set from the route by the endpoint, so a mismatched body id
    /// cannot redirect the decision at another quotation.</summary>
    public Guid Id { get; init; }

    /// <summary>Unlike a rejection's reason, this is never required — an
    /// approval needs no justification to be valid, so the popup that asks
    /// for it must never block on an empty box.</summary>
    public string? Note { get; init; }
}

public sealed class ApproveBoqCommandHandler(
    IApplicationDbContext context,
    ICurrentUser actor,
    TimeProvider timeProvider)
    : IRequestHandler<ApproveBoqCommand, ApiResult<BoqDto>>
{
    public async Task<ApiResult<BoqDto>> Handle(
        ApproveBoqCommand request,
        CancellationToken cancellationToken)
    {
        var boq = await context.Boqs
            .Include(x => x.Sections).ThenInclude(x => x.Lines)
            .Include(x => x.ApprovalEvents)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (boq is null)
            return ApiResult<BoqDto>.Failure("Quotation not found.");

        // Refused rather than treated as a no-op: two approvers looking at the
        // same queue must not both come away believing theirs was the decision.
        if (!BoqWorkflow.CanDecide(boq.Status))
            return ApiResult<BoqDto>.Failure(
                boq.Status == BoqStatus.Approved
                    ? $"This quotation was already approved by {boq.ApprovedByName ?? "somebody else"}."
                    : "Only a quotation submitted for approval can be approved.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        BoqWorkflow.Approve(boq, actor, now);
        BoqWorkflow.Record(context, boq, BoqApprovalAction.Approved, actor, now, note);
        boq.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<BoqDto>.Success(BoqMappings.ToDto(boq));
    }
}
