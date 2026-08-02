using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.VipClients.Queries.DownloadVipDocument;

public sealed record DownloadVipDocumentQuery(Guid DocumentId)
    : IRequest<ApiResult<VipDocumentContent>>;

public sealed class DownloadVipDocumentQueryHandler(
    IApplicationDbContext context,
    IFileStorage storage,
    ICurrentUser currentUser)
    : IRequestHandler<DownloadVipDocumentQuery, ApiResult<VipDocumentContent>>
{
    public async Task<ApiResult<VipDocumentContent>> Handle(
        DownloadVipDocumentQuery request,
        CancellationToken cancellationToken)
    {
        var document = await context.VipClientDocuments
            .AsNoTracking()
            .Include(d => d.Folder)
                .ThenInclude(f => f.VipClient)
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken);

        if (document is null)
            return ApiResult<VipDocumentContent>.Failure("Document not found.");

        // A client may only read their own project. Staff reach this through a
        // permission-gated route, so this check is what stops one client
        // guessing another's document id. "Not found" rather than "forbidden":
        // confirming the id exists would leak that another project holds it.
        if (currentUser.Role == Roles.Client
            && document.Folder.VipClient.AdminUserId != currentUser.UserId)
        {
            return ApiResult<VipDocumentContent>.Failure("Document not found.");
        }

        var content = await storage.OpenReadAsync(document.StorageKey, cancellationToken);
        if (content is null)
            return ApiResult<VipDocumentContent>.Failure("The stored file is missing.");

        return ApiResult<VipDocumentContent>.Success(
            new VipDocumentContent(content, document.FileName, document.ContentType));
    }
}
