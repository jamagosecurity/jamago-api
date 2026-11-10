using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Maintenance.Queries.GetMaintenanceRequests;

/// <summary>Bound with [AsParameters] — every optional field must be
/// nullable, same reasoning as GetBoqsQuery. Gated on maintenance.manage at
/// the endpoint: this is the company-wide queue, not "my tickets" — see
/// GetMyMaintenanceRequestsQuery for that.</summary>
public sealed record GetMaintenanceRequestsQuery : IRequest<ApiResult<PaginatedResult<MaintenanceRequestListItemDto>>>
{
    public int? PageNumber { get; init; }
    public int? PageSize { get; init; }
    /// <summary>Matches on ticket number, site, client name, or any line's issue.</summary>
    public string? Search { get; init; }
    public MaintenanceStatus? Status { get; init; }
    public string? Category { get; init; }
    public bool? UnassignedOnly { get; init; }
}

public sealed class GetMaintenanceRequestsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetMaintenanceRequestsQuery, ApiResult<PaginatedResult<MaintenanceRequestListItemDto>>>
{
    public async Task<ApiResult<PaginatedResult<MaintenanceRequestListItemDto>>> Handle(
        GetMaintenanceRequestsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.PageNumber ?? 1);
        var size = Math.Clamp(request.PageSize ?? 20, 1, 100);

        var query = context.MaintenanceRequests.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(x => x.RequestNumber.ToLower().Contains(search)
                || (x.SiteLocation != null && x.SiteLocation.ToLower().Contains(search))
                || (x.ClientName != null && x.ClientName.ToLower().Contains(search))
                || x.Lines.Any(l => l.IssueType.ToLower().Contains(search)));
        }

        if (request.Status is { } status)
            query = query.Where(x => x.Status == status);

        // A ticket belongs to every category one of its lines falls under.
        if (MaintenanceCategories.Normalize(request.Category) is { Length: > 0 } category)
            query = query.Where(x => x.Lines.Any(l => l.Category == category));

        if (request.UnassignedOnly == true)
            query = query.Where(x => x.AssignedToId == null);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(x => x.Lines)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MaintenanceMappings.ToListItemDto).ToList();
        var totalPages = size == 0 ? 0 : (int)Math.Ceiling(total / (double)size);

        return ApiResult<PaginatedResult<MaintenanceRequestListItemDto>>.Success(
            new PaginatedResult<MaintenanceRequestListItemDto>(dtos, total, page, size, totalPages));
    }
}
