using Jama.Application.ClientRegistrations.Commands.ApproveClientRegistration;
using Jama.Application.ClientRegistrations.Commands.RegisterClient;
using Jama.Application.ClientRegistrations.Commands.RejectClientRegistration;
using Jama.Application.ClientRegistrations.Queries.GetClientRegistrations;
using Jama.Application.ClientRegistrations.Queries.GetMyClientRegistration;
using Jama.Application.ClientRegistrations.Queries.GetPendingClientRegistrationCount;
using Jama.Domain.Enums;
using Jama.Web.Infrastructure;
using MediatR;
using AppRoles = Jama.Application.Common.Roles;

namespace Jama.Web.Endpoints;

/// <summary>
/// Clients signing themselves up, and admins deciding on them.
///
/// Sign-up is the one anonymous route, rate-limited per IP. Review is the
/// Admin role only — the decision opens a login and, for an MOI client, a DIA
/// record, which is administrator business rather than a grant handed to staff.
/// The client's own route resolves the registration from the token.
/// </summary>
public sealed class ClientRegistrations : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapPost(Register, "register", rateLimitPolicy: RateLimitPolicies.ClientRegister)
            .MapGet(GetMine, "me", AppRoles.Client)
            .MapGet(GetAll, roles: AppRoles.Admin)
            .MapGet(GetPendingCount, "pending-count", AppRoles.Admin)
            .MapPost(Approve, "{id:guid}/approve", AppRoles.Admin)
            .MapPost(Reject, "{id:guid}/reject", AppRoles.Admin);
    }

    public async Task<IResult> Register(
        ISender sender,
        RegisterClientCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> GetMine(ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMyClientRegistrationQuery(), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> GetAll(
        ISender sender,
        ClientRegistrationStatus? status,
        string? search,
        CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetClientRegistrationsQuery(status, search), cancellationToken));

    public async Task<IResult> GetPendingCount(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetPendingClientRegistrationCountQuery(), cancellationToken));

    public async Task<IResult> Approve(
        ISender sender,
        Guid id,
        ApproveClientRegistrationCommand command,
        CancellationToken cancellationToken)
    {
        // The route id wins over anything in the body.
        var result = await sender.Send(command with { Id = id }, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Reject(
        ISender sender,
        Guid id,
        RejectClientRegistrationCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with { Id = id }, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }
}
