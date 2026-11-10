using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Maintenance.Queries.GetMaintenanceCatalogueImage;

public sealed record MaintenanceImageContentDto(Stream Content, string ContentType, string FileName);

public sealed record GetMaintenanceCatalogueImageQuery(Guid ImageId) : IRequest<ApiResult<MaintenanceImageContentDto>>;

public sealed class GetMaintenanceCatalogueImageQueryHandler(IApplicationDbContext context, IFileStorage storage)
    : IRequestHandler<GetMaintenanceCatalogueImageQuery, ApiResult<MaintenanceImageContentDto>>
{
    public async Task<ApiResult<MaintenanceImageContentDto>> Handle(
        GetMaintenanceCatalogueImageQuery request, CancellationToken cancellationToken)
    {
        var image = await context.MaintenanceCatalogueImages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ImageId, cancellationToken);

        if (image is null)
            return ApiResult<MaintenanceImageContentDto>.Failure("Image not found.");

        var content = await storage.OpenReadAsync(image.StorageKey, cancellationToken);
        if (content is null)
            return ApiResult<MaintenanceImageContentDto>.Failure("Image content is missing.");

        return ApiResult<MaintenanceImageContentDto>.Success(
            new MaintenanceImageContentDto(content, image.ContentType, image.FileName));
    }
}
