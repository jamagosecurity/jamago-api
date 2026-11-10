using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Maintenance.Commands.DeleteMaintenanceCatalogueItem;

/// <summary>Takes a service off the Non-MOI price list. Safe for tickets that
/// already used it — their lines hold their own copy of the name and price.</summary>
public sealed record DeleteMaintenanceCatalogueItemCommand(Guid Id) : IRequest<ApiResult<string>>;

public sealed class DeleteMaintenanceCatalogueItemCommandHandler(IApplicationDbContext context, IFileStorage storage)
    : IRequestHandler<DeleteMaintenanceCatalogueItemCommand, ApiResult<string>>
{
    public async Task<ApiResult<string>> Handle(
        DeleteMaintenanceCatalogueItemCommand request, CancellationToken cancellationToken)
    {
        // Images are loaded so their files go too: the database cascade drops
        // the rows but knows nothing about the bytes in storage.
        var item = await context.MaintenanceCatalogueItems
            .Include(i => i.Images)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);
        if (item is null)
            return ApiResult<string>.Failure("Service not found.");

        var storageKeys = item.Images.Select(i => i.StorageKey).ToList();

        context.MaintenanceCatalogueItems.Remove(item);
        await context.SaveChangesAsync(cancellationToken);

        foreach (var key in storageKeys)
            await storage.DeleteAsync(key, cancellationToken);

        return ApiResult<string>.Success("Service removed.");
    }
}
