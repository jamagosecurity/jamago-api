using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Maintenance.Commands.DeleteMaintenanceCatalogueImage;

/// <summary>Removes one picture from a service on the Non-MOI price list.</summary>
public sealed record DeleteMaintenanceCatalogueImageCommand(Guid ImageId) : IRequest<ApiResult<Guid>>;

public sealed class DeleteMaintenanceCatalogueImageCommandHandler(IApplicationDbContext context, IFileStorage storage)
    : IRequestHandler<DeleteMaintenanceCatalogueImageCommand, ApiResult<Guid>>
{
    public async Task<ApiResult<Guid>> Handle(
        DeleteMaintenanceCatalogueImageCommand request, CancellationToken cancellationToken)
    {
        var image = await context.MaintenanceCatalogueImages
            .FirstOrDefaultAsync(x => x.Id == request.ImageId, cancellationToken);

        if (image is null)
            return ApiResult<Guid>.Failure("Image not found.");

        context.MaintenanceCatalogueImages.Remove(image);
        await context.SaveChangesAsync(cancellationToken);

        // Row first, bytes second: a failure between them leaves an unreferenced
        // file (wasted disk) rather than a row pointing at nothing (a broken image).
        await storage.DeleteAsync(image.StorageKey, cancellationToken);

        return ApiResult<Guid>.Success(image.Id);
    }
}
