using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Drawings.Commands.DeleteDrawing;

public sealed record DeleteDrawingCommand(Guid Id) : IRequest<ApiResult<Guid>>;

public sealed class DeleteDrawingCommandHandler(IApplicationDbContext context, IFileStorage storage)
    : IRequestHandler<DeleteDrawingCommand, ApiResult<Guid>>
{
    public async Task<ApiResult<Guid>> Handle(
        DeleteDrawingCommand request,
        CancellationToken cancellationToken)
    {
        var drawing = await context.Drawings
            .Include(x => x.Files)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (drawing is null)
            return ApiResult<Guid>.Failure("Drawing not found.");

        // The database rows go with the drawing through the configured cascade;
        // the files on disk do not follow a foreign key and have to be removed
        // one by one, or the drawing's row disappears while its DWGs and PDFs
        // sit orphaned in storage forever.
        var keys = drawing.Files.Select(f => f.StorageKey).ToList();

        context.Drawings.Remove(drawing);
        await context.SaveChangesAsync(cancellationToken);

        foreach (var key in keys)
            await storage.DeleteAsync(key, cancellationToken);

        return ApiResult<Guid>.Success(drawing.Id);
    }
}
