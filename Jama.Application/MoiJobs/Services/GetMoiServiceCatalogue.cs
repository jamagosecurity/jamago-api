using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Application.Maintenance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.MoiJobs.Services;

/// <summary>The MOI price list, in category order then the order admin
/// added each service. Open to any signed-in account: the checklist a client
/// or technician fills in is built from it.</summary>
public sealed record GetMoiServiceCatalogueQuery : IRequest<ApiResult<IReadOnlyList<MaintenanceIssueOptionDto>>>;

public sealed class GetMoiServiceCatalogueQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetMoiServiceCatalogueQuery, ApiResult<IReadOnlyList<MaintenanceIssueOptionDto>>>
{
    public async Task<ApiResult<IReadOnlyList<MaintenanceIssueOptionDto>>> Handle(
        GetMoiServiceCatalogueQuery request, CancellationToken cancellationToken)
    {
        var items = await context.MoiServiceItems
            .AsNoTracking()
            .Include(i => i.Images)
            .ToListAsync(cancellationToken);

        // Sorted here rather than in the query: the section order is the
        // checklist's, which is not the alphabetical order the column sorts in.
        var ordered = items
            .OrderBy(i => MoiServiceCategories.Rank(i.Category))
            .ThenBy(i => i.Category)
            .ThenBy(i => i.SortOrder)
            .ThenBy(i => i.Name)
            .Select(MoiServiceRules.ToDto)
            .ToList();

        return ApiResult<IReadOnlyList<MaintenanceIssueOptionDto>>.Success(ordered);
    }
}
