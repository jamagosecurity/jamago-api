using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Drawings.Queries.GetDrawings;

/// <summary>
/// Bound with [AsParameters]. Every optional parameter must be nullable — see
/// GetBoqsQuery for why.
/// </summary>
public sealed record GetDrawingsQuery : IRequest<ApiResult<PaginatedResult<DrawingListItemDto>>>
{
    public int? PageNumber { get; init; }
    public int? PageSize { get; init; }
    /// <summary>Matches on drawing number, project or client.</summary>
    public string? Search { get; init; }
    public DrawingStatus? Status { get; init; }
    /// <summary>True to list only the signed-in user's own drawings.</summary>
    public bool? MineOnly { get; init; }
}

public sealed class GetDrawingsQueryHandler(IApplicationDbContext context, ICurrentUser actor)
    : IRequestHandler<GetDrawingsQuery, ApiResult<PaginatedResult<DrawingListItemDto>>>
{
    public async Task<ApiResult<PaginatedResult<DrawingListItemDto>>> Handle(
        GetDrawingsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.PageNumber ?? 1);
        var size = Math.Clamp(request.PageSize ?? 20, 1, 100);

        var query = context.Drawings.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(x => x.DrawingNumber.ToLower().Contains(search)
                || x.ProjectName.ToLower().Contains(search)
                || (x.ClientName != null && x.ClientName.ToLower().Contains(search)));
        }

        if (request.Status is { } status)
            query = query.Where(x => x.Status == status);

        // Privacy boundary, not a convenience filter — see the Boq list for why.
        if (!actor.Has(Permissions.DrawingApprove))
            query = query.Where(x => x.Status == DrawingStatus.Approved || x.PreparedById == actor.UserId);

        if (request.MineOnly == true)
            query = query.Where(x => x.PreparedById == actor.UserId);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new DrawingListItemDto(
                x.Id,
                x.DrawingNumber,
                x.ProjectName,
                x.ClientName,
                x.ContactNumber,
                x.Status,
                x.PreparedByName,
                x.SubmittedAt,
                x.ApprovedByName,
                x.ApprovedAt,
                x.RejectedByName,
                x.RejectedAt,
                x.RejectionReason,
                x.Files.Count,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        var totalPages = size == 0 ? 0 : (int)Math.Ceiling(total / (double)size);

        return ApiResult<PaginatedResult<DrawingListItemDto>>.Success(
            new PaginatedResult<DrawingListItemDto>(items, total, page, size, totalPages));
    }
}
