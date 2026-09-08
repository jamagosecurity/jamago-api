using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Drawings.Commands.ApproveDrawing;

/// <summary>
/// Approves a drawing that was submitted for approval. Gated on
/// drawing.approve at the endpoint.
/// </summary>
public sealed record ApproveDrawingCommand(Guid Id) : IRequest<ApiResult<DrawingDto>>;

public sealed class ApproveDrawingCommandHandler(
    IApplicationDbContext context,
    ICurrentUser actor,
    TimeProvider timeProvider)
    : IRequestHandler<ApproveDrawingCommand, ApiResult<DrawingDto>>
{
    public async Task<ApiResult<DrawingDto>> Handle(
        ApproveDrawingCommand request,
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
                drawing.Status == DrawingStatus.Approved
                    ? $"This drawing was already approved by {drawing.ApprovedByName ?? "somebody else"}."
                    : "Only a drawing submitted for approval can be approved.");

        var now = timeProvider.GetUtcNow().UtcDateTime;

        DrawingWorkflow.Approve(drawing, actor, now);
        DrawingWorkflow.Record(context, drawing, DrawingApprovalAction.Approved, actor, now);
        drawing.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<DrawingDto>.Success(DrawingMappings.ToDto(drawing));
    }
}
