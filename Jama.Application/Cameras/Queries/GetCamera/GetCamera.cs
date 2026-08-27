using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Cameras.Queries.GetCamera;

public sealed record GetCameraQuery(Guid Id) : IRequest<ApiResult<CameraDto>>;

public sealed class GetCameraQueryHandler(IApplicationDbContext context)
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

        return camera is null
            ? ApiResult<CameraDto>.Failure("Camera not found.")
            : ApiResult<CameraDto>.Success(camera);
    }
}
