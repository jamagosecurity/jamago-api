using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Jama.Application.Auth;
using Jama.Application.Auth.Commands.Login;
using Jama.Application.Auth.Queries.GetCurrentUser;
using Jama.Application.Common.Models;
using Jama.Web.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Jama.Web.Endpoints;

public class Auth : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapPost(Login, "login")
            .MapGet(Me, "me", requireAuthorization: true);
    }

    public async Task<Results<Ok<TypedResult<LoginResponse>>, JsonHttpResult<TypedResult<LoginResponse>>, StatusCodeHttpResult>> Login(
        ISender sender,
        LoginCommand command)
    {
        try
        {
            var result = await sender.Send(command);
            if (!result.Succeeded)
            {
                return TypedResults.Json(result, statusCode: StatusCodes.Status401Unauthorized);
            }

            return TypedResults.Ok(result);
        }
        catch (Exception ex) when (
            ex is InvalidOperationException
            or TimeoutException
            or Npgsql.NpgsqlException)
        {
            return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    public async Task<Results<Ok<TypedResult<UserSummaryDto>>, UnauthorizedHttpResult>> Me(
        ISender sender,
        ClaimsPrincipal user)
    {
        var userIdValue = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var result = await sender.Send(new GetCurrentUserQuery { UserId = userId });
        if (!result.Succeeded)
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(result);
    }
}
