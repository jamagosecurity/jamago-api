using Jama.Application.Common.Interfaces;
using Jama.Application.Geo;

namespace Jama.Infrastructure.Geo;

/// <summary>
/// Follows a short Google Maps share link (maps.app.goo.gl/…) to the address it redirects to, and reads the pin from that.
///
/// Redirects are followed by hand, a few hops at most, and EVERY hop must be an https Google Maps host (see
/// <see cref="MapLinks.IsAllowedHost"/>) before it is requested. The server therefore never fetches an address a user chose,
/// only Google's own — a pasted link cannot be used to make the server call something else.
/// </summary>
public sealed class MapLinkResolver(HttpClient http) : IMapLinkResolver
{
    private const int MaxHops = 5;

    public async Task<MapPinDto?> ResolveAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var current)) return null;

        for (var hop = 0; hop <= MaxHops; hop++)
        {
            if (current.Scheme != Uri.UriSchemeHttps || !MapLinks.IsAllowedHost(current.Host)) return null;

            var pin = MapLinks.TryParse(current.ToString());
            if (pin is not null) return pin;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                var next = response.Headers.Location;
                if (next is null) return null;
                current = next.IsAbsoluteUri ? next : new Uri(current, next);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return null;
            }
        }

        return null;
    }
}
