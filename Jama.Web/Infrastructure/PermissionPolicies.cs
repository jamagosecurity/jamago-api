using Jama.Application.Common;
using Microsoft.AspNetCore.Authorization;

namespace Jama.Web.Infrastructure;

/// <summary>
/// Registers one authorization policy per permission, named after the permission
/// key itself. Endpoints opt in with <c>RequirePermission(Permissions.DiaUpload)</c>,
/// which keeps authorization declarative instead of scattering claim checks
/// through handlers.
///
/// The Admin role satisfies every policy on its own, without needing the matching
/// claim. Admins are already granted all permissions when a token is minted, so
/// this changes nothing about who may do what — but it means a token issued
/// before a permission existed, or before permission claims shipped at all, does
/// not lock an administrator out of their own console until they re-authenticate.
/// An endpoint moving from role-gated to permission-gated is otherwise a silent
/// breaking change for every session already in flight.
/// </summary>
public static class PermissionPolicies
{
    public static AuthorizationBuilder AddPermissionPolicies(this AuthorizationBuilder builder)
    {
        foreach (var permission in Permissions.All)
        {
            var key = permission.Key;
            builder.AddPolicy(key, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context =>
                    context.User.IsInRole(Roles.Admin)
                    || context.User.HasClaim(PermissionClaims.Type, key)));
        }

        return builder;
    }
}
