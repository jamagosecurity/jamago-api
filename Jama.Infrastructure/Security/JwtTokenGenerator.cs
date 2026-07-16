using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Jama.Application.Auth;
using Jama.Application.Options;
using Jama.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Jama.Infrastructure.Security;

public class JwtTokenGenerator(IOptions<JwtSettings> options) : ITokenGenerator
{
    public LoginResponse Generate(AdminUser user)
    {
        var settings = options.Value;
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(settings.AccessTokenMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new LoginResponse(
            accessToken,
            expiresAtUtc,
            new UserSummaryDto(user.Id, user.Email, user.FullName, user.Role));
    }
}
