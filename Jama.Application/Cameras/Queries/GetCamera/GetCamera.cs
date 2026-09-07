using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Cameras.Queries.GetCamera;

public sealed record GetCameraQuery(Guid Id) : IRequest<ApiResult<CameraDto>>;

public sealed class GetCameraQueryHandler(IApplicationDbContext context, ICurrentUser actor)
    : IRequestHandler<GetCameraQuery, ApiResult<CameraDto>>
{
    public async Task<ApiResult<CameraDto>> Handle(
        GetCameraQuery request,
        CancellationToken cancellationToken)
    {
        var camera = await context.Cameras
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .Select(CameraMappings.Projection)
            .FirstOrDefaultAsync(cancellationToken);

        if (camera is null)
            return ApiResult<CameraDto>.Failure("Camera not found.");

        // Same rule as the list: the detail of a public catalogue item is public,
        // what it cost us is not.
        return ApiResult<CameraDto>.Success(
            actor.Has(Permissions.CostView) ? camera : CameraMappings.WithoutCost(camera));
    }
}
