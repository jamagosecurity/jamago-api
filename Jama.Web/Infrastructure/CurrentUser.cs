using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Jama.Application.Common.Interfaces;

namespace Jama.Web.Infrastructure;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
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
}
