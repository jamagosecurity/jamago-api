using Jama.Application.Common;
using Jama.Application.Dashboard;
using Jama.Web.Infrastructure;
using MediatR;

namespace Jama.Web.Endpoints;

/// <summary>The super admin's dashboard. Revenue is on it, so only the root account may read it.</summary>
public sealed class Dashboard : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapGet(Get, permission: AuthorizationPolicies.SuperAdmin);
    }

    public async Task<IResult> Get(ISender sender, CancellationToken ct) =>
        Results.Ok(await sender.Send(new GetDashboardQuery(), ct));
}
