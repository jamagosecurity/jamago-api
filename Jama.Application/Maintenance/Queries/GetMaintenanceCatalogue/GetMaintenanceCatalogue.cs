using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Maintenance.Queries.GetMaintenanceCatalogue;

/// <summary>The Non-MOI price list, in category order then the order admin
/// added each service. Open to any signed-in account: the checklist a client
/// or technician fills in is built from it.</summary>
public sealed record GetMaintenanceCatalogueQuery : IRequest<ApiResult<IReadOnlyList<MaintenanceIssueOptionDto>>>;

public sealed class GetMaintenanceCatalogueQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetMaintenanceCatalogueQuery, ApiResult<IReadOnlyList<MaintenanceIssueOptionDto>>>
{
    public async Task<ApiResult<IReadOnlyList<MaintenanceIssueOptionDto>>> Handle(
        GetMaintenanceCatalogueQuery request, CancellationToken cancellationToken)
    {
        var items = await context.MaintenanceCatalogueItems
            .AsNoTracking()
            .Include(i => i.Images)
            .ToListAsync(cancellationToken);

        // Sorted here rather than in the query: the section order is the
        // checklist's, which is not the alphabetical order the column sorts in.
        var ordered = items
            .OrderBy(i => MaintenanceCategories.Rank(i.Category))
            .ThenBy(i => i.Category)
            .ThenBy(i => i.SortOrder)
            .ThenBy(i => i.Name)
            .Select(MaintenanceCatalogueRules.ToDto)
            .ToList();

        return ApiResult<IReadOnlyList<MaintenanceIssueOptionDto>>.Success(ordered);
    }
}
