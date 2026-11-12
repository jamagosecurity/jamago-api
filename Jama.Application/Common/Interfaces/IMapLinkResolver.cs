using Jama.Application.Geo;

namespace Jama.Application.Common.Interfaces;

/// <summary>Finds the pin behind a Google Maps link, opening a short share link to see where it leads when it has to.</summary>
public interface IMapLinkResolver
{
    Task<MapPinDto?> ResolveAsync(string url, CancellationToken cancellationToken);
}
