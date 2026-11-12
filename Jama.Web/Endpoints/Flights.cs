using Jama.Application.Flights;
using Jama.Web.Infrastructure;
using MediatR;

namespace Jama.Web.Endpoints;

/// <summary>Live aircraft over Qatar for the sign-in page's map. Public (the page is shown before sign-in) and served from
/// a shared 45-second cache, so however often it is called, OpenSky is asked at most once every 45 seconds.</summary>
public sealed class Flights : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapGet(GetQatar, "qatar");
    }

    public async Task<IResult> GetQatar(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetQatarFlightsQuery(), cancellationToken));
}
