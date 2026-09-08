using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Drawings.Commands.RejectDrawing;

/// <summary>
/// Rejects a drawing that was submitted for approval, with the reason. Required
/// by the server, not just by the modal that asks for it: a rework starts from
/// the reason.
/// </summary>
public sealed record RejectDrawingCommand : IRequest<ApiResult<DrawingDto>>
{
    /// <summary>Set from the route by the endpoint.</summary>
    public Guid Id { get; init; }

    public string? Reason { get; init; }
}

public sealed class RejectDrawingCommandHandler(
    IApplicationDbContext context,
    ICurrentUser actor,
    TimeProvider timeProvider)
    : IRequestHandler<RejectDrawingCommand, ApiResult<DrawingDto>>
{
    public async Task<ApiResult<DrawingDto>> Handle(
        RejectDrawingCommand request,
        CancellationToken cancellationToken)
    {
        var drawing = await context.Drawings
            .Include(x => x.Files)
            .Include(x => x.ApprovalEvents)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (drawing is null)
            return ApiResult<DrawingDto>.Failure("Drawing not found.");

        if (!DrawingWorkflow.CanDecide(drawing.Status))
            return ApiResult<DrawingDto>.Failure(
                drawing.Status == DrawingStatus.Rejected
                    ? "This drawing has already been rejected."
                    : "Only a drawing submitted for approval can be rejected.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var reason = request.Reason!.Trim();

        DrawingWorkflow.Reject(drawing, actor, now, reason);
        DrawingWorkflow.Record(context, drawing, DrawingApprovalAction.Rejected, actor, now, reason);
        drawing.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<DrawingDto>.Success(DrawingMappings.ToDto(drawing));
    }
}
