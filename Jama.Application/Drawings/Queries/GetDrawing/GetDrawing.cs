using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Drawings.Queries.GetDrawing;

public sealed record GetDrawingQuery(Guid Id) : IRequest<ApiResult<DrawingDto>>;

public sealed class GetDrawingQueryHandler(IApplicationDbContext context, ICurrentUser actor)
    : IRequestHandler<GetDrawingQuery, ApiResult<DrawingDto>>
{
    public async Task<ApiResult<DrawingDto>> Handle(
        GetDrawingQuery request,
        CancellationToken cancellationToken)
    {
        var drawing = await context.Drawings
            .AsNoTracking()
            .Include(x => x.Files)
            .Include(x => x.ApprovalEvents)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (drawing is null || !DrawingVisibility.CanSee(drawing.Status, drawing.PreparedById, actor))
            return ApiResult<DrawingDto>.Failure("Drawing not found.");

        return ApiResult<DrawingDto>.Success(DrawingMappings.ToDto(drawing));
    }
}
