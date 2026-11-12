using Jama.Application.Geo;
using Jama.Web.Infrastructure;
using MediatR;

namespace Jama.Web.Endpoints;

/// <summary>Small map helpers for the forms that pin a site. Any signed-in user may use them.</summary>
public sealed class Geo : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapGet(Resolve, "resolve", requireAuthorization: true);
    }

    public async Task<IResult> Resolve(ISender sender, string? url, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new ResolveMapLinkQuery(url), cancellationToken));
}
