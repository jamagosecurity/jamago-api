using Jama.Application.Common.Models;
using MediatR;

namespace Jama.Application.Flights;

/// <summary>One aircraft over Qatar, as the sign-in page's map draws it.</summary>
public sealed record FlightDto(
    string Id,
    string Callsign,
    string Country,
    double Latitude,
    double Longitude,
    double? AltitudeMetres,
    double? HeadingDegrees,
    double? SpeedMetresPerSecond,
    bool OnGround);

/// <summary>Live aircraft positions over Qatar (OpenSky Network), shared between visitors and refreshed at most every
/// <see cref="FlightFeedRules.CacheFor"/>.</summary>
public interface IFlightFeed
{
    Task<IReadOnlyList<FlightDto>> GetQatarAsync(CancellationToken cancellationToken);
}

public static class FlightFeedRules
{
    public static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(45);
}

public sealed record GetQatarFlightsQuery : IRequest<ApiResult<IReadOnlyList<FlightDto>>>;

public sealed class GetQatarFlightsQueryHandler(IFlightFeed feed)
    : IRequestHandler<GetQatarFlightsQuery, ApiResult<IReadOnlyList<FlightDto>>>
{
    public async Task<ApiResult<IReadOnlyList<FlightDto>>> Handle(GetQatarFlightsQuery request, CancellationToken cancellationToken) =>
        ApiResult<IReadOnlyList<FlightDto>>.Success(await feed.GetQatarAsync(cancellationToken));
}
