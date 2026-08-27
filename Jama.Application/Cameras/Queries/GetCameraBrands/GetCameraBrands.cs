using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Cameras.Queries.GetCameraBrands;

public sealed record CameraBrandCountDto(string Brand, int ItemCount);

/// <summary>
/// The brands actually present in the catalogue, with how many lines each has.
///
/// Read from the data rather than from a fixed list: brand is free text, so the
/// only truthful answer to "which brands can I filter by" is whichever ones
/// someone has entered.
/// </summary>
public sealed record GetCameraBrandsQuery : IRequest<ApiResult<IReadOnlyList<CameraBrandCountDto>>>;

public sealed class GetCameraBrandsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCameraBrandsQuery, ApiResult<IReadOnlyList<CameraBrandCountDto>>>
{
    public async Task<ApiResult<IReadOnlyList<CameraBrandCountDto>>> Handle(
        GetCameraBrandsQuery request,
        CancellationToken cancellationToken)
    {
        // Grouped into an anonymous type, not straight into the DTO: EF cannot
        // translate a projection into a record constructor and throws at runtime.
        var grouped = await context.Cameras
            .AsNoTracking()
            .GroupBy(x => x.Brand)
            .Select(g => new { Brand = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var brands = grouped
            .OrderBy(x => x.Brand, StringComparer.OrdinalIgnoreCase)
            .Select(x => new CameraBrandCountDto(x.Brand, x.Count))
            .ToList();

        return ApiResult<IReadOnlyList<CameraBrandCountDto>>.Success(brands);
    }
}
