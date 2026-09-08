using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Drawings.Commands.DeleteDrawingFile;

public sealed record DeleteDrawingFileCommand(Guid FileId) : IRequest<ApiResult<Guid>>;

public sealed class DeleteDrawingFileCommandHandler(
    IApplicationDbContext context,
    IFileStorage storage,
    ICurrentUser actor)
    : IRequestHandler<DeleteDrawingFileCommand, ApiResult<Guid>>
{
    public async Task<ApiResult<Guid>> Handle(
        DeleteDrawingFileCommand request,
        CancellationToken cancellationToken)
    {
        var file = await context.DrawingFiles
            .Include(x => x.Drawing)
            .FirstOrDefaultAsync(x => x.Id == request.FileId, cancellationToken);

        if (file is null)
            return ApiResult<Guid>.Failure("File not found.");

        var amending = !DrawingWorkflow.IsEditable(file.Drawing.Status);
        if (amending && !actor.IsSuperAdmin)
            return ApiResult<Guid>.Failure(
                "An approved drawing can only be changed by the super administrator.");

        // Storage first: if the disk delete throws, the database row — and the
        // download link — should still point at something. Deleting the row
        // first and then failing to remove the file would leave an orphan on
        // disk with nothing to ever clean it up.
        await storage.DeleteAsync(file.StorageKey, cancellationToken);

        context.DrawingFiles.Remove(file);
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<Guid>.Success(request.FileId);
    }
}
