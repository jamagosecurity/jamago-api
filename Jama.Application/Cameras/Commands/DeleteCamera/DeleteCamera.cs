using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Cameras.Commands.DeleteCamera;

public sealed record DeleteCameraCommand(Guid Id) : IRequest<ApiResult<Guid>>;

public sealed class DeleteCameraCommandHandler(IApplicationDbContext context, IFileStorage storage)
    : IRequestHandler<DeleteCameraCommand, ApiResult<Guid>>
{
    public async Task<ApiResult<Guid>> Handle(
        DeleteCameraCommand request,
        CancellationToken cancellationToken)
    {
        // Images are loaded so their files can be removed too. The database
        // cascade drops the CameraImage rows on its own, but it knows nothing
        // about the bytes on disk — without this, deleting an item leaves its
        // pictures behind permanently, referenced by nothing.
        var camera = await context.Cameras
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (camera is null)
            return ApiResult<Guid>.Failure("Camera not found.");

        var storageKeys = camera.Images.Select(x => x.StorageKey).ToList();

        context.Cameras.Remove(camera);
        await context.SaveChangesAsync(cancellationToken);

        // Rows first, bytes second: a failure here leaves unreferenced files,
        // which is wasted disk. The other order would leave rows pointing at
        // nothing, which is a broken image on the page.
        foreach (var key in storageKeys)
        {
            await storage.DeleteAsync(key, cancellationToken);
        }

        return ApiResult<Guid>.Success(camera.Id);
    }
}
