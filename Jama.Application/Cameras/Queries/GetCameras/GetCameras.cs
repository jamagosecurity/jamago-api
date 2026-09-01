using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Cameras.Queries.GetCameras;

/// <summary>
/// Bound with [AsParameters]. Every optional parameter must be nullable: a
/// non-nullable value type is treated as a REQUIRED query parameter and the
/// property initialiser is never consulted, so a missing page number would 500
/// rather than default. Defaults are applied in the handler instead.
/// </summary>
public sealed record GetCamerasQuery : IRequest<ApiResult<PaginatedResult<CameraDto>>>
{
    public int? PageNumber { get; init; }
    public int? PageSize { get; init; }

    /// <summary>Matches on item name, brand, model number, search key or HSN code.</summary>
    public string? Search { get; init; }

    public string? Type { get; init; }
    public ProductCategory? Category { get; init; }

    /// <summary>Exact brand, matched case-insensitively — the value comes from
    /// /api/cameras/brands, so it is one the data actually contains.</summary>
    public string? Brand { get; init; }

    /// <summary>True to list only items at or below their low-stock threshold.</summary>
    public bool? LowStockOnly { get; init; }
}

public sealed class GetCamerasQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCamerasQuery, ApiResult<PaginatedResult<CameraDto>>>
{
    public async Task<ApiResult<PaginatedResult<CameraDto>>> Handle(
        GetCamerasQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.PageNumber ?? 1);
        var size = Math.Clamp(request.PageSize ?? 20, 1, 100);

        var query = context.Cameras.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // The search key exists precisely so an item can be found by words
            // that are not in its name — trade names, alternate part numbers —
            // so it is searched alongside the rest.
            var search = request.Search.Trim().ToLower();
            query = query.Where(x => x.ItemName.ToLower().Contains(search)
                || x.Brand.ToLower().Contains(search)
                || x.ModelNo.ToLower().Contains(search)
                || (x.SearchKey != null && x.SearchKey.ToLower().Contains(search))
                || (x.HsnCode != null && x.HsnCode.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            var type = request.Type.Trim().ToLower();
            query = query.Where(x => x.Type.ToLower() == type);
        }

        if (request.Category is { } category)
            query = query.Where(x => x.Category == category);

        if (!string.IsNullOrWhiteSpace(request.Brand))
        {
            var brand = request.Brand.Trim().ToLower();
            query = query.Where(x => x.Brand.ToLower() == brand);
        }

        if (request.LowStockOnly == true)
            query = query.Where(x => x.LowStock != null && x.Quantity <= x.LowStock);

        // Counted before paging so the client can size its pager, and both the
        // count and the page run against the same filtered query.
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.ItemName)
            .ThenBy(x => x.Brand)
            .ThenBy(x => x.ModelNo)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(CameraMappings.Projection)
            .ToListAsync(cancellationToken);

        var totalPages = size == 0 ? 0 : (int)Math.Ceiling(total / (double)size);

        return ApiResult<PaginatedResult<CameraDto>>.Success(
            new PaginatedResult<CameraDto>(items, total, page, size, totalPages));
    }
}
