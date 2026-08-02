using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.VipClients.Commands.DeleteVipClient;

public sealed record DeleteVipClientCommand(Guid Id) : IRequest<ApiResult<Guid>>;

public sealed class DeleteVipClientCommandHandler(IApplicationDbContext context, IFileStorage storage)
    : IRequestHandler<DeleteVipClientCommand, ApiResult<Guid>>
{
    public async Task<ApiResult<Guid>> Handle(
        DeleteVipClientCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.VipClients
            .Include(x => x.Account)
            .Include(x => x.Folders)
                .ThenInclude(f => f.Documents)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
            return ApiResult<Guid>.Failure("VIP client not found.");

        var keys = entity.Folders.SelectMany(f => f.Documents).Select(d => d.StorageKey).ToList();

        // Rows first: if a file delete fails we would rather leave an orphaned
        // file on disk than a row pointing at content that no longer exists.
        context.VipClients.Remove(entity);
        context.AdminUsers.Remove(entity.Account);
        await context.SaveChangesAsync(cancellationToken);

        foreach (var key in keys)
            await storage.DeleteAsync(key, cancellationToken);

        return ApiResult<Guid>.Success(entity.Id);
    }
}
