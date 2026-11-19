using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.MoiJobs.Services;

/// <summary>Takes a service off the MOI price list. Safe for tickets that
/// already used it — their lines hold their own copy of the name and price.</summary>
public sealed record DeleteMoiServiceItemCommand(Guid Id) : IRequest<ApiResult<string>>;

public sealed class DeleteMoiServiceItemCommandHandler(IApplicationDbContext context, IFileStorage storage)
    : IRequestHandler<DeleteMoiServiceItemCommand, ApiResult<string>>
{
    public async Task<ApiResult<string>> Handle(
        DeleteMoiServiceItemCommand request, CancellationToken cancellationToken)
    {
        // Images are loaded so their files go too: the database cascade drops
        // the rows but knows nothing about the bytes in storage.
        var item = await context.MoiServiceItems
            .Include(i => i.Images)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);
        if (item is null)
            return ApiResult<string>.Failure("Service not found.");

        var storageKeys = item.Images.Select(i => i.StorageKey).ToList();

        context.MoiServiceItems.Remove(item);
        await context.SaveChangesAsync(cancellationToken);

        foreach (var key in storageKeys)
            await storage.DeleteAsync(key, cancellationToken);

        return ApiResult<string>.Success("Service removed.");
    }
}
