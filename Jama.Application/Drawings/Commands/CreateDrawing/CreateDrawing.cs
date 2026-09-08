using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using MediatR;

namespace Jama.Application.Drawings.Commands.CreateDrawing;

public sealed record CreateDrawingCommand : IRequest<ApiResult<DrawingDto>>, IDrawingWrite
{
    public string? ProjectName { get; init; }
    public string? ClientName { get; init; }
    public string? SiteLocation { get; init; }
    public string? ContactNumber { get; init; }
    public string? Notes { get; init; }
}

public sealed class CreateDrawingCommandHandler(
    IApplicationDbContext context,
    ICurrentUser actor,
    TimeProvider timeProvider)
    : IRequestHandler<CreateDrawingCommand, ApiResult<DrawingDto>>
{
    public async Task<ApiResult<DrawingDto>> Handle(
        CreateDrawingCommand request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var drawing = new Drawing
        {
            Id = Guid.CreateVersion7(),
            DrawingNumber = await DrawingNumbers.NextAsync(context, cancellationToken),
            // Taken from the token, never the request: who drafted it is not
            // something the caller gets to assert.
            PreparedById = actor.UserId,
            PreparedByName = actor.DisplayName,
            CreatedAt = now,
        };

        DrawingWriteRules.Apply(drawing, request);

        context.Drawings.Add(drawing);
        DrawingWorkflow.Record(context, drawing, DrawingApprovalAction.Created, actor, now);

        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<DrawingDto>.Success(DrawingMappings.ToDto(drawing));
    }
}
