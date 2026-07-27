using Jama.Application.Common;
using Microsoft.AspNetCore.Authorization;

namespace Jama.Web.Infrastructure;

/// <summary>
/// Registers one authorization policy per permission, named after the permission
/// key itself. Endpoints opt in with <c>RequirePermission(Permissions.DiaUpload)</c>,
/// which keeps authorization declarative instead of scattering claim checks
/// through handlers.
/// </summary>
public static class PermissionPolicies
{
    public static AuthorizationBuilder AddPermissionPolicies(this AuthorizationBuilder builder)
    {
        foreach (var permission in Permissions.All)
        {
            builder.AddPolicy(permission.Key, policy =>
                policy.RequireClaim(PermissionClaims.Type, permission.Key));
        }

        return builder;
    }
}
