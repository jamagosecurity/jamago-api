using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Drawings.Commands.SubmitDrawing;

/// <summary>
/// Hands a drawing to whoever may approve it. Open to anyone who can build
/// one — submitting is the last step of drafting, not a decision about the
/// document.
/// </summary>
public sealed record SubmitDrawingCommand(Guid Id) : IRequest<ApiResult<DrawingDto>>;

public sealed class SubmitDrawingCommandHandler(
    IApplicationDbContext context,
    ICurrentUser actor,
    TimeProvider timeProvider)
    : IRequestHandler<SubmitDrawingCommand, ApiResult<DrawingDto>>
{
    public async Task<ApiResult<DrawingDto>> Handle(
        SubmitDrawingCommand request,
        CancellationToken cancellationToken)
    {
        var drawing = await context.Drawings
            .Include(x => x.Files)
            .Include(x => x.ApprovalEvents)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (drawing is null)
            return ApiResult<DrawingDto>.Failure("Drawing not found.");

        if (!DrawingWorkflow.CanSubmit(drawing.Status))
            return ApiResult<DrawingDto>.Failure(
                drawing.Status == DrawingStatus.Submitted
                    ? "This drawing is already waiting for approval."
                    : "An approved drawing cannot be submitted again.");

        // Nothing for an approver to look at otherwise — a bare DWG cannot be
        // opened on screen, so a submission with no plotted PDF is one nobody
        // can actually review.
        if (drawing.Files.Count == 0)
            return ApiResult<DrawingDto>.Failure("Add at least one file before submitting for approval.");

        if (!drawing.Files.Any(f => f.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)))
            return ApiResult<DrawingDto>.Failure(
                "Add a plotted PDF before submitting — DWG and DXF cannot be opened on screen, "
                + "so the approver needs something they can actually look at.");

        var now = timeProvider.GetUtcNow().UtcDateTime;

        DrawingWorkflow.Submit(drawing, now);
        DrawingWorkflow.Record(context, drawing, DrawingApprovalAction.Submitted, actor, now);
        drawing.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<DrawingDto>.Success(DrawingMappings.ToDto(drawing));
    }
}
