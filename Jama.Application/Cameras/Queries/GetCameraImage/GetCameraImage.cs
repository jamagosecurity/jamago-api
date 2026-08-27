using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Cameras.Queries.GetCameraImage;

public sealed record CameraImageContentDto(Stream Content, string ContentType, string FileName);

public sealed record GetCameraImageQuery(Guid ImageId) : IRequest<ApiResult<CameraImageContentDto>>;

public sealed class GetCameraImageQueryHandler(IApplicationDbContext context, IFileStorage storage)
    : IRequestHandler<GetCameraImageQuery, ApiResult<CameraImageContentDto>>
{
    public async Task<ApiResult<CameraImageContentDto>> Handle(
        GetCameraImageQuery request,
        CancellationToken cancellationToken)
    {
        var image = await context.CameraImages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ImageId, cancellationToken);

        if (image is null)
            return ApiResult<CameraImageContentDto>.Failure("Image not found.");

        var content = await storage.OpenReadAsync(image.StorageKey, cancellationToken);
        if (content is null)
            return ApiResult<CameraImageContentDto>.Failure("Image content is missing.");

        return ApiResult<CameraImageContentDto>.Success(
            new CameraImageContentDto(content, image.ContentType, image.FileName));
    }
}
