using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.MoiJobs.Services;

public sealed record MoiServiceImageContentDto(Stream Content, string ContentType, string FileName);

public sealed record GetMoiServiceImageQuery(Guid ImageId) : IRequest<ApiResult<MoiServiceImageContentDto>>;

public sealed class GetMoiServiceImageQueryHandler(IApplicationDbContext context, IFileStorage storage)
    : IRequestHandler<GetMoiServiceImageQuery, ApiResult<MoiServiceImageContentDto>>
{
    public async Task<ApiResult<MoiServiceImageContentDto>> Handle(
        GetMoiServiceImageQuery request, CancellationToken cancellationToken)
    {
        var image = await context.MoiServiceImages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ImageId, cancellationToken);

        if (image is null)
            return ApiResult<MoiServiceImageContentDto>.Failure("Image not found.");

        var content = await storage.OpenReadAsync(image.StorageKey, cancellationToken);
        if (content is null)
            return ApiResult<MoiServiceImageContentDto>.Failure("Image content is missing.");

        return ApiResult<MoiServiceImageContentDto>.Success(
            new MoiServiceImageContentDto(content, image.ContentType, image.FileName));
    }
}
