using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Drawings.Commands.UpdateDrawing;

public sealed record UpdateDrawingCommand : IRequest<ApiResult<DrawingDto>>, IDrawingWrite
{
    /// <summary>Set from the route by the endpoint, so a mismatched body id
    /// cannot redirect the write at another drawing.</summary>
    public Guid Id { get; init; }

    public string? ProjectName { get; init; }
    public string? ClientName { get; init; }
    public string? SiteLocation { get; init; }
    public string? ContactNumber { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpdateDrawingCommandHandler(
    IApplicationDbContext context,
    ICurrentUser actor,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateDrawingCommand, ApiResult<DrawingDto>>
{
    public async Task<ApiResult<DrawingDto>> Handle(
        UpdateDrawingCommand request,
        CancellationToken cancellationToken)
    {
        var drawing = await context.Drawings
            .Include(x => x.Files)
            .Include(x => x.ApprovalEvents)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (drawing is null)
            return ApiResult<DrawingDto>.Failure("Drawing not found.");

        // Only an approval closes a drawing to edits — see DrawingWorkflow. The
        // super administrator is the exception to that close, on the same terms
        // as an approved quotation.
        var amending = !DrawingWorkflow.IsEditable(drawing.Status);

        if (amending && !actor.IsSuperAdmin)
            return ApiResult<DrawingDto>.Failure(
                "An approved drawing can only be edited by the super administrator.");

        DrawingWriteRules.Apply(drawing, request);

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Appended, never in place of the decision: the approval stands, and so
        // does the fact that the document changed after it.
        if (amending)
            DrawingWorkflow.Record(context, drawing, DrawingApprovalAction.Amended, actor, now);

        drawing.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<DrawingDto>.Success(DrawingMappings.ToDto(drawing));
    }
}
