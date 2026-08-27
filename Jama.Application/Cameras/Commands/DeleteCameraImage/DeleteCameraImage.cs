using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Cameras.Commands.DeleteCameraImage;

public sealed record DeleteCameraImageCommand(Guid ImageId) : IRequest<ApiResult<Guid>>;

public sealed class DeleteCameraImageCommandHandler(IApplicationDbContext context, IFileStorage storage)
    : IRequestHandler<DeleteCameraImageCommand, ApiResult<Guid>>
{
    public async Task<ApiResult<Guid>> Handle(
        DeleteCameraImageCommand request,
        CancellationToken cancellationToken)
    {
        var image = await context.CameraImages
            .FirstOrDefaultAsync(x => x.Id == request.ImageId, cancellationToken);

        if (image is null)
            return ApiResult<Guid>.Failure("Image not found.");

        context.CameraImages.Remove(image);
        await context.SaveChangesAsync(cancellationToken);

        // Row first, bytes second: a delete that fails here leaves an unreferenced
        // file, which is wasted disk. The other order would leave a row pointing
        // at nothing, which is a broken image on the page.
        await storage.DeleteAsync(image.StorageKey, cancellationToken);

        return ApiResult<Guid>.Success(image.Id);
    }
}
