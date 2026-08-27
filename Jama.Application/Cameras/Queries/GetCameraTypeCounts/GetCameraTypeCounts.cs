using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Cameras.Queries.GetCameraTypeCounts;

public sealed record CameraTypeCountDto(CameraType Type, int ItemCount, int UnitCount);

/// <summary>
/// How many stock lines and units sit under each camera type.
///
/// Feeds the public catalogue's sections: every type is listed with its count
/// before anything is expanded, so a visitor can see there are nine bullets
/// without first opening the bullet section. Types with nothing in them are
/// returned too, with zeroes — the catalogue decides whether to show them, and
/// a missing key would be indistinguishable from a failed request.
/// </summary>
public sealed record GetCameraTypeCountsQuery : IRequest<ApiResult<IReadOnlyList<CameraTypeCountDto>>>;

public sealed class GetCameraTypeCountsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCameraTypeCountsQuery, ApiResult<IReadOnlyList<CameraTypeCountDto>>>
{
    public async Task<ApiResult<IReadOnlyList<CameraTypeCountDto>>> Handle(
        GetCameraTypeCountsQuery request,
        CancellationToken cancellationToken)
    {
        // One grouped round trip rather than a count per type.
        var grouped = await context.Cameras
            .AsNoTracking()
            .GroupBy(x => x.Type)
            .Select(g => new { Type = g.Key, Items = g.Count(), Units = g.Sum(x => x.Quantity) })
            .ToListAsync(cancellationToken);

        var counts = Enum.GetValues<CameraType>()
            .Select(type =>
            {
                var match = grouped.FirstOrDefault(x => x.Type == type);
                return new CameraTypeCountDto(type, match?.Items ?? 0, match?.Units ?? 0);
            })
            .ToList();

        return ApiResult<IReadOnlyList<CameraTypeCountDto>>.Success(counts);
    }
}
