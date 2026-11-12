using System.Globalization;
using System.Text.RegularExpressions;

namespace Jama.Application.Geo;

/// <summary>A place on the map, as read out of a Google Maps link.</summary>
public sealed record MapPinDto(double Latitude, double Longitude);

/// <summary>
/// Reads a pin out of a Google Maps link, and says which hosts are safe to follow when a link has to be opened to find it
/// (a phone's "Share" link is a short maps.app.goo.gl address that only redirects to the real one).
/// </summary>
public static class MapLinks
{
    private const string Num = @"-?\d+(?:\.\d+)?";

    /// <summary>Ordered on purpose: the exact place marker (!3d…!4d…) beats the map's centre (@…), which only says where the
    /// view was looking.</summary>
    private static readonly Regex[] Patterns =
    [
        new($"!3d({Num})!4d({Num})", RegexOptions.Compiled),
        new($"@({Num}),({Num})", RegexOptions.Compiled),
        new($"/place/({Num}),({Num})", RegexOptions.Compiled),
        new($"[?&](?:q|query|ll|sll|center|destination|daddr|mlat)=({Num})(?:,|%2C)({Num})", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    ];

    private static readonly Regex GoogleHost =
        new(@"^(?:www\.|maps\.)?google\.(?:com|[a-z]{2}|com\.[a-z]{2}|co\.[a-z]{2})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static MapPinDto? TryParse(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        foreach (var pattern in Patterns)
        {
            var match = pattern.Match(url);
            if (!match.Success) continue;

            var latitude = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var longitude = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            if (Math.Abs(latitude) > 90 || Math.Abs(longitude) > 180 || (latitude == 0 && longitude == 0)) continue;

            return new MapPinDto(Math.Round(latitude, 6), Math.Round(longitude, 6));
        }

        return null;
    }

    /// <summary>The only hosts a link may point at while being followed. Exact matches, so "google.com.evil.com" and
    /// "maps.app.goo.gl.evil.com" are refused.</summary>
    public static bool IsAllowedHost(string? host) =>
        !string.IsNullOrEmpty(host)
        && (host.Equals("maps.app.goo.gl", StringComparison.OrdinalIgnoreCase)
            || host.Equals("goo.gl", StringComparison.OrdinalIgnoreCase)
            || GoogleHost.IsMatch(host));
}
