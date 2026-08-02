using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.VipClients.Queries.GetVipClients;

/// <summary>Archived projects are excluded by default; true lists only those.</summary>
public sealed record GetVipClientsQuery(bool Archived = false)
    : IRequest<ApiResult<IReadOnlyList<VipClientListItemDto>>>;

public sealed class GetVipClientsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetVipClientsQuery, ApiResult<IReadOnlyList<VipClientListItemDto>>>
{
    public async Task<ApiResult<IReadOnlyList<VipClientListItemDto>>> Handle(
        GetVipClientsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await context.VipClients
            .AsNoTracking()
            .Where(x => x.IsArchived == request.Archived)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new VipClientListItemDto(
                x.Id,
                x.ClientName,
                x.ProjectName,
                x.FolderName,
                x.Account.Email,
                x.IsActive,
                x.Account.IsActive,
                x.Folders.SelectMany(f => f.Documents).Count(),
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return ApiResult<IReadOnlyList<VipClientListItemDto>>.Success(items);
    }
}
