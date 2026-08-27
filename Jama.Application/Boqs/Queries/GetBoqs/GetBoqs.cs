using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Boqs.Queries.GetBoqs;

/// <summary>
/// Bound with [AsParameters]. Every optional parameter must be nullable: a
/// non-nullable value type is treated as a REQUIRED query parameter and the
/// property initialiser is never consulted.
/// </summary>
public sealed record GetBoqsQuery : IRequest<ApiResult<PaginatedResult<BoqListItemDto>>>
{
    public int? PageNumber { get; init; }
    public int? PageSize { get; init; }
    /// <summary>Matches on BOQ number, project, site or client.</summary>
    public string? Search { get; init; }
    public BoqStatus? Status { get; init; }
    /// <summary>True to list only the signed-in user's own BOQs.</summary>
    public bool? MineOnly { get; init; }
}

public sealed class GetBoqsQueryHandler(IApplicationDbContext context, ICurrentUser actor)
    : IRequestHandler<GetBoqsQuery, ApiResult<PaginatedResult<BoqListItemDto>>>
{
    public async Task<ApiResult<PaginatedResult<BoqListItemDto>>> Handle(
        GetBoqsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.PageNumber ?? 1);
        var size = Math.Clamp(request.PageSize ?? 20, 1, 100);

        var query = context.Boqs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(x => x.BoqNumber.ToLower().Contains(search)
                || x.ProjectName.ToLower().Contains(search)
                || (x.SiteLocation != null && x.SiteLocation.ToLower().Contains(search))
                || (x.ClientName != null && x.ClientName.ToLower().Contains(search)));
        }

        if (request.Status is { } status)
            query = query.Where(x => x.Status == status);

        if (request.MineOnly == true)
            query = query.Where(x => x.PreparedById == actor.UserId);

        var total = await query.CountAsync(cancellationToken);

        // Newest first: a BOQ list is a worklist, and the one just written is the
        // one most likely to be wanted again.
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new BoqListItemDto(
                x.Id,
                x.BoqNumber,
                x.ProjectName,
                x.SiteLocation,
                x.ClientName,
                x.IssueDate,
                x.Status,
                x.Total,
                x.Sections.Count,
                x.Sections.Sum(s => s.Lines.Count),
                x.PreparedByName,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        var totalPages = size == 0 ? 0 : (int)Math.Ceiling(total / (double)size);

        return ApiResult<PaginatedResult<BoqListItemDto>>.Success(
            new PaginatedResult<BoqListItemDto>(items, total, page, size, totalPages));
    }
}
