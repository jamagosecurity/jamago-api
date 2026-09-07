using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Options;
using Microsoft.Extensions.Options;

namespace Jama.Web.Infrastructure;

public sealed class CurrentUser(
    IHttpContextAccessor accessor,
    IOptions<AdminSeedSettings> adminSeed) : ICurrentUser
{
    private ClaimsPrincipal User => accessor.HttpContext?.User
        ?? throw new UnauthorizedAccessException("An authenticated user is required.");

    public Guid UserId
    {
        get
        {
            var value = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id)
                ? id
                : throw new UnauthorizedAccessException("The access token has no valid user identifier.");
        }
    }

    public string? DisplayName
    {
        get
        {
            var name = User.FindFirstValue(ClaimTypes.Name);
            var email = User.FindFirstValue(JwtRegisteredClaimNames.Email)
                ?? User.FindFirstValue(ClaimTypes.Email);
            return (name, email) switch
            {
                ({ Length: > 0 }, { Length: > 0 }) => $"{name} <{email}>",
                ({ Length: > 0 }, _) => name,
                (_, { Length: > 0 }) => email,
                _ => null,
            };
        }
    }

    public string? Role => User.FindFirstValue(ClaimTypes.Role);

    /// <summary>
    /// Read off the token, the same way the endpoint policies read it — so a
    /// handler and its route gate can never disagree about what the caller holds.
    ///
    /// The Admin role satisfies any permission on its own, exactly as every
    /// policy in PermissionPolicies does. Admins are minted carrying every claim,
    /// so this changes nothing about who may do what — but a token issued BEFORE
    /// a permission existed does not carry that permission's claim, and without
    /// this an administrator signed in across the deployment that added one would
    /// silently lose the field it guards until they signed in again. That is
    /// exactly how supplier cost stopped saving.
    ///
    /// An anonymous caller has no principal to ask, and holds nothing.
    /// </summary>
    /// <summary>
    /// Read from the same place the SuperAdmin policy reads it — the email claim
    /// against AdminSeed:Email — so the handler and the policy cannot disagree
    /// about who the root account is.
    /// </summary>
    public bool IsSuperAdmin
    {
        get
        {
            var user = accessor.HttpContext?.User;
            if (user is null || !user.IsInRole(Roles.Admin)) return false;

            var email = user.FindFirstValue(JwtRegisteredClaimNames.Email)
                ?? user.FindFirstValue(ClaimTypes.Email);

            return adminSeed.Value.IsSuperAdmin(email);
        }
    }

    public bool Has(string permission)
    {
        var user = accessor.HttpContext?.User;
        if (user is null) return false;

        return user.IsInRole(Roles.Admin) || user.HasClaim(PermissionClaims.Type, permission);
    }
}
