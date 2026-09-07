using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Boqs.Commands.RejectBoq;

/// <summary>
/// Rejects a quotation that was submitted for approval, with the reason.
///
/// The reason is required, and required by the server rather than only by the
/// modal that asks for it: "rejected" on its own tells the person who built it
/// nothing they can act on, and a rework starts from the reason.
/// </summary>
public sealed record RejectBoqCommand : IRequest<ApiResult<BoqDto>>
{
    /// <summary>Set from the route by the endpoint, so a mismatched body id
    /// cannot redirect the decision at another quotation.</summary>
    public Guid Id { get; init; }

    public string? Reason { get; init; }
}

public sealed class RejectBoqCommandHandler(
    IApplicationDbContext context,
    ICurrentUser actor,
    TimeProvider timeProvider)
    : IRequestHandler<RejectBoqCommand, ApiResult<BoqDto>>
{
    public async Task<ApiResult<BoqDto>> Handle(
        RejectBoqCommand request,
        CancellationToken cancellationToken)
    {
        var boq = await context.Boqs
            .Include(x => x.Sections).ThenInclude(x => x.Lines)
            .Include(x => x.ApprovalEvents)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (boq is null)
            return ApiResult<BoqDto>.Failure("Quotation not found.");

        if (!BoqWorkflow.CanDecide(boq.Status))
            return ApiResult<BoqDto>.Failure(
                boq.Status == BoqStatus.Rejected
                    ? "This quotation has already been rejected."
                    : "Only a quotation submitted for approval can be rejected.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var reason = request.Reason!.Trim();

        BoqWorkflow.Reject(boq, actor, now, reason);
        BoqWorkflow.Record(context, boq, BoqApprovalAction.Rejected, actor, now, reason);
        boq.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<BoqDto>.Success(BoqMappings.ToDto(boq));
    }
}
