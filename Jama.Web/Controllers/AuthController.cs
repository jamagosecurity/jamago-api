using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Jama.Application.DTOs;
using Jama.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jama.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var result = await auth.LoginAsync(request, ct);
            if (result is null)
            {
                return Unauthorized(new { message = "Invalid email or password." });
            }

            return Ok(result);
        }
        catch (Exception ex) when (
            ex is InvalidOperationException
            or TimeoutException
            or Npgsql.NpgsqlException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Database is unavailable. Check PostgreSQL is running and ConnectionStrings:DefaultConnection username/password.",
            });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserSummaryDto>> Me(CancellationToken ct = default)
    {
        var userIdValue = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        var user = await auth.GetUserAsync(userId, ct);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(user);
    }
}
