using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;

namespace Jama.Application.Geo;

/// <summary>Turns a pasted Google Maps link — including a phone's short share link — into a latitude and longitude.</summary>
public sealed record ResolveMapLinkQuery(string? Url) : IRequest<ApiResult<MapPinDto>>;

public sealed class ResolveMapLinkQueryHandler(IMapLinkResolver resolver)
    : IRequestHandler<ResolveMapLinkQuery, ApiResult<MapPinDto>>
{
    public async Task<ApiResult<MapPinDto>> Handle(ResolveMapLinkQuery request, CancellationToken cancellationToken)
    {
        var url = request.Url?.Trim();
        if (string.IsNullOrEmpty(url) || url.Length > 500)
            return ApiResult<MapPinDto>.Failure("Paste a Google Maps link.");

        var pin = await resolver.ResolveAsync(url, cancellationToken);
        return pin is null
            ? ApiResult<MapPinDto>.Failure("Could not read a location from that link. Open it in Maps, long-press the pin, then copy the numbers.")
            : ApiResult<MapPinDto>.Success(pin);
    }
}
