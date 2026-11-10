using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Maintenance.Queries.GetMyMaintenanceRequests;

/// <summary>The signed-in account's own tickets — whatever they filed
/// themselves or were assigned, client, staff or technician alike. Open to any
/// authenticated account.</summary>
public sealed record GetMyMaintenanceRequestsQuery : IRequest<ApiResult<IReadOnlyList<MaintenanceRequestListItemDto>>>;

public sealed class GetMyMaintenanceRequestsQueryHandler(IApplicationDbContext context, ICurrentUser actor)
    : IRequestHandler<GetMyMaintenanceRequestsQuery, ApiResult<IReadOnlyList<MaintenanceRequestListItemDto>>>
{
    public async Task<ApiResult<IReadOnlyList<MaintenanceRequestListItemDto>>> Handle(
        GetMyMaintenanceRequestsQuery request, CancellationToken cancellationToken)
    {
        var items = await context.MaintenanceRequests
            .AsNoTracking()
            .Include(x => x.Lines)
            // A technician's own list has to include the jobs handed to them,
            // not only the ones they logged themselves.
            .Where(x => x.ReportedById == actor.UserId || x.AssignedToId == actor.UserId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return ApiResult<IReadOnlyList<MaintenanceRequestListItemDto>>.Success(
            items.Select(MaintenanceMappings.ToListItemDto).ToList());
    }
}
