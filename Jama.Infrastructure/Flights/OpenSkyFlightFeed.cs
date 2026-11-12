using System.Text.Json;
using Jama.Application.Flights;

namespace Jama.Infrastructure.Flights;

/// <summary>
/// Reads aircraft over Qatar from OpenSky Network's public API. One fetch serves every visitor for 45 seconds, so the
/// number of calls does not grow with the number of people on the page. Anything that goes wrong — OpenSky slow, down or
/// rate-limited — answers with the last good list (or none): the map simply shows fewer planes, it never shows an error.
/// </summary>
public sealed class OpenSkyFlightFeed(HttpClient http, TimeProvider time) : IFlightFeed
{
    private const string Url = "https://opensky-network.org/api/states/all?lamin=24.3&lomin=50.4&lamax=26.5&lomax=52.2";

    private readonly SemaphoreSlim gate = new(1, 1);
    private IReadOnlyList<FlightDto> last = [];
    private DateTimeOffset fetchedAt = DateTimeOffset.MinValue;

    public async Task<IReadOnlyList<FlightDto>> GetQatarAsync(CancellationToken cancellationToken)
    {
        if (time.GetUtcNow() - fetchedAt < FlightFeedRules.CacheFor) return last;

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (time.GetUtcNow() - fetchedAt < FlightFeedRules.CacheFor) return last;   // someone else just fetched
            fetchedAt = time.GetUtcNow();                                               // even a failure waits its turn

            using var response = await http.GetAsync(Url, cancellationToken);
            if (!response.IsSuccessStatusCode) return last;
            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            last = Parse(await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken));
            return last;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return last;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>OpenSky's "state vectors": arrays of [icao24, callsign, country, time, contact, lon, lat, baro alt, on ground,
    /// velocity, track, …]. Rows without a position are skipped.</summary>
    public static IReadOnlyList<FlightDto> Parse(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("states", out var states) || states.ValueKind != JsonValueKind.Array) return [];

        var flights = new List<FlightDto>();
        foreach (var row in states.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 11) continue;
            double? Number(int i) => row[i].ValueKind == JsonValueKind.Number ? row[i].GetDouble() : null;
            string Text(int i) => row[i].ValueKind == JsonValueKind.String ? row[i].GetString()!.Trim() : string.Empty;

            var lon = Number(5);
            var lat = Number(6);
            if (lat is null || lon is null) continue;

            flights.Add(new FlightDto(
                Text(0), Text(1), Text(2), Math.Round(lat.Value, 5), Math.Round(lon.Value, 5),
                Number(7), Number(10), Number(9), row[8].ValueKind == JsonValueKind.True));
        }
        return flights;
    }
}
