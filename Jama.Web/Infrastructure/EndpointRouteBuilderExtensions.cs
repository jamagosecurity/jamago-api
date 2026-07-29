using Jama.Application.Common;
using Jama.Web.Endpoints;
using Microsoft.AspNetCore.Authorization;

namespace Jama.Web.Infrastructure;

public static class EndpointRouteBuilderExtensions
{
    public static RouteGroupBuilder MapGroup(this WebApplication app, EndpointGroupBase group)
    {
        // Lowercase routes so they match the Angular client (/api/auth, /api/staff, /api/contacts).
        var groupName = group.GetType().Name.ToLowerInvariant();
        return app.MapGroup($"/api/{groupName}")
            .WithTags(groupName);
    }

    public static RouteGroupBuilder MapGet(
        this RouteGroupBuilder builder,
        Delegate handler,
        string pattern = "",
        string? roles = null,
        bool requireAuthorization = false,
        string? permission = null)
    {
        var route = builder.MapGet(pattern, handler);
        route.Protect(roles, requireAuthorization, permission);
        return builder;
    }

    /// <summary>
    /// Applies role, permission or plain authentication requirements to a route.
    ///
    /// Role and permission compose rather than override: supplying both requires
    /// the caller to satisfy each. That is what lets the technician endpoints
    /// demand the Technician role (the portal boundary) *and* dia.inspect (the
    /// capability), so revoking the capability does not also have to mean
    /// changing someone's department.
    ///
    /// A permission on its own is satisfied by the policy of the same name, which
    /// admins always meet because their token carries every permission claim.
    /// </summary>
    private static void Protect(
        this RouteHandlerBuilder route,
        string? roles,
        bool requireAuthorization,
        string? permission)
    {
        var hasRoles = !string.IsNullOrWhiteSpace(roles);
        var hasPermission = !string.IsNullOrWhiteSpace(permission);

        if (hasRoles && hasPermission)
        {
            route.RequireAuthorization(policy => policy
                .RequireRole(roles!)
                .RequireClaim(PermissionClaims.Type, permission!));
            return;
        }

        if (hasPermission)
        {
            route.RequireAuthorization(permission!);
            return;
        }

        if (hasRoles)
        {
            route.RequireAuthorization(policy => policy.RequireRole(roles!));
            return;
        }

        if (requireAuthorization)
        {
            route.RequireAuthorization();
        }
    }

    public static RouteGroupBuilder MapPost(
        this RouteGroupBuilder builder,
        Delegate handler,
        string pattern = "",
        string? roles = null,
        bool requireAuthorization = false,
        string? permission = null)
    {
        var route = builder.MapPost(pattern, handler);
        route.Protect(roles, requireAuthorization, permission);
        return builder;
    }

    public static RouteGroupBuilder MapPut(
        this RouteGroupBuilder builder,
        Delegate handler,
        string pattern = "",
        string? roles = null,
        bool requireAuthorization = false,
        string? permission = null)
    {
        var route = builder.MapPut(pattern, handler);
        route.Protect(roles, requireAuthorization, permission);
        return builder;
    }

    public static RouteGroupBuilder MapDelete(
        this RouteGroupBuilder builder,
        Delegate handler,
        string pattern = "",
        string? roles = null,
        bool requireAuthorization = false,
        string? permission = null)
    {
        var route = builder.MapDelete(pattern, handler);
        route.Protect(roles, requireAuthorization, permission);
        return builder;
    }
}
