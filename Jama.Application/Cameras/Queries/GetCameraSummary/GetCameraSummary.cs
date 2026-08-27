using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Cameras.Queries.GetCameraSummary;

/// <summary>
/// Totals across the WHOLE inventory, not the current page.
///
/// The list endpoint pages, so a client adding up the rows it holds can only
/// ever report the page. These three figures are what the inventory header
/// wants to state, so they are computed in SQL where every row is in scope.
/// </summary>
public sealed record CameraSummaryDto(
    int TotalLines,
    int TotalUnits,
    int BrandCount,
    /// <summary>Items at or below their own low-stock threshold. Items with no
    /// threshold set are never counted — nobody asked to be warned about them.</summary>
    int LowStockCount,
    /// <summary>Retail value of everything held: rate x quantity, summed. Lines
    /// with no rate contribute nothing rather than blocking the total.</summary>
    decimal StockValue);

public sealed record GetCameraSummaryQuery : IRequest<ApiResult<CameraSummaryDto>>;

public sealed class GetCameraSummaryQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCameraSummaryQuery, ApiResult<CameraSummaryDto>>
{
    public async Task<ApiResult<CameraSummaryDto>> Handle(
        GetCameraSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var cameras = context.Cameras.AsNoTracking();

        var totalLines = await cameras.CountAsync(cancellationToken);

        // SumAsync over no rows throws on a non-nullable selector, so the cast
        // makes an empty inventory return null and coalesce to zero.
        var totalUnits = await cameras.SumAsync(x => (int?)x.Quantity, cancellationToken) ?? 0;

        // Case-insensitive so "Hikvision" and "hikvision" are not counted twice.
        var brandCount = await cameras
            .Select(x => x.Brand.ToLower())
            .Distinct()
            .CountAsync(cancellationToken);

        var lowStockCount = await cameras
            .CountAsync(x => x.LowStock != null && x.Quantity <= x.LowStock, cancellationToken);

        var stockValue = await cameras
            .SumAsync(x => (decimal?)((x.Rate ?? 0m) * x.Quantity), cancellationToken) ?? 0m;

        return ApiResult<CameraSummaryDto>.Success(
            new CameraSummaryDto(
                totalLines,
                totalUnits,
                brandCount,
                lowStockCount,
                Math.Round(stockValue, 2, MidpointRounding.AwayFromZero)));
    }
}
