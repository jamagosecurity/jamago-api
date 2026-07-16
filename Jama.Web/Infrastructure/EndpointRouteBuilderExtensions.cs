using Jama.Web.Endpoints;
using Microsoft.AspNetCore.Authorization;

namespace Jama.Web.Infrastructure;

public static class EndpointRouteBuilderExtensions
{
    public static RouteGroupBuilder MapGroup(this WebApplication app, EndpointGroupBase group)
    {
        var groupName = group.GetType().Name;
        return app.MapGroup($"/api/{groupName}")
            .WithTags(groupName);
    }

    public static RouteGroupBuilder MapGet(
        this RouteGroupBuilder builder,
        Delegate handler,
        string pattern = "",
        string? roles = null,
        bool requireAuthorization = false)
    {
        var route = builder.MapGet(pattern, handler);
        if (!string.IsNullOrWhiteSpace(roles))
        {
            route.RequireAuthorization(policy => policy.RequireRole(roles));
        }
        else if (requireAuthorization)
        {
            route.RequireAuthorization();
        }

        return builder;
    }

    public static RouteGroupBuilder MapPost(
        this RouteGroupBuilder builder,
        Delegate handler,
        string pattern = "",
        string? roles = null)
    {
        var route = builder.MapPost(pattern, handler);
        if (!string.IsNullOrWhiteSpace(roles))
        {
            route.RequireAuthorization(policy => policy.RequireRole(roles));
        }

        return builder;
    }

    public static RouteGroupBuilder MapPut(
        this RouteGroupBuilder builder,
        Delegate handler,
        string pattern = "",
        string? roles = null)
    {
        var route = builder.MapPut(pattern, handler);
        if (!string.IsNullOrWhiteSpace(roles))
        {
            route.RequireAuthorization(policy => policy.RequireRole(roles));
        }

        return builder;
    }

    public static RouteGroupBuilder MapDelete(
        this RouteGroupBuilder builder,
        Delegate handler,
        string pattern = "",
        string? roles = null)
    {
        var route = builder.MapDelete(pattern, handler);
        if (!string.IsNullOrWhiteSpace(roles))
        {
            route.RequireAuthorization(policy => policy.RequireRole(roles));
        }

        return builder;
    }
}
