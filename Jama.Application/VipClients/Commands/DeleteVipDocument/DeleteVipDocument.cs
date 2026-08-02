using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.VipClients.Commands.DeleteVipDocument;

public sealed record DeleteVipDocumentCommand(Guid DocumentId) : IRequest<ApiResult<Guid>>;

public sealed class DeleteVipDocumentCommandHandler(IApplicationDbContext context, IFileStorage storage)
    : IRequestHandler<DeleteVipDocumentCommand, ApiResult<Guid>>
{
    public async Task<ApiResult<Guid>> Handle(
        DeleteVipDocumentCommand request,
        CancellationToken cancellationToken)
    {
        var document = await context.VipClientDocuments
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken);

        if (document is null)
            return ApiResult<Guid>.Failure("Document not found.");

        var key = document.StorageKey;
        context.VipClientDocuments.Remove(document);
        await context.SaveChangesAsync(cancellationToken);

        await storage.DeleteAsync(key, cancellationToken);
        return ApiResult<Guid>.Success(document.Id);
    }
}
