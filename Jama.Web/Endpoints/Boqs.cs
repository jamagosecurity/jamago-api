using Jama.Application.Boqs.Commands.ApproveBoq;
using Jama.Application.Boqs.Commands.CreateBoq;
using Jama.Application.Boqs.Commands.DeleteBoq;
using Jama.Application.Boqs.Commands.RejectBoq;
using Jama.Application.Boqs.Commands.SeenBoqNotifications;
using Jama.Application.Boqs.Commands.SubmitBoq;
using Jama.Application.Boqs.Commands.UpdateBoq;
using Jama.Application.Boqs.Queries.GetBoq;
using Jama.Application.Boqs.Queries.GetBoqPdf;
using Jama.Application.Boqs.Queries.GetBoqNotifications;
using Jama.Application.Boqs.Queries.GetBoqs;
using Jama.Application.Common;
using Jama.Application.Common.Models;
using Jama.Web.Infrastructure;
using MediatR;

namespace Jama.Web.Endpoints;

/// <summary>
/// Bills of quantities, assembled from the stock catalogue.
///
/// Two grants, on purpose. boq.manage builds a quotation and submits it;
/// boq.approve answers it. Reads accept either, because an approver has to open
/// the document to decide on it — see AuthorizationPolicies.BoqRead. Admins hold
/// both implicitly, and can grant each to individual staff.
///
/// The split is enforced here rather than by which buttons the client draws: a
/// request straight to /approve from an account without the grant is refused by
/// the policy, whatever the screen it came from was showing.
///
/// Rates are not part of the contract. A line names a stock item and a quantity;
/// the server reads the price from the catalogue. Staff choose what and how
/// many, an administrator decides what it costs, and no request can cross that
/// line — see BoqWriter.
/// </summary>
public sealed class Boqs : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapGet(GetAll, permission: AuthorizationPolicies.BoqRead)
            // Anyone who may see a quotation may be told about decisions on it.
            // WHICH decisions is the query's business, not the policy's: an
            // administrator is told about everything, everybody else about their
            // own work and their own decisions. Route constraints keep these
            // clear of "{id:guid}".
            .MapGet(GetNotifications, "notifications", permission: AuthorizationPolicies.BoqRead)
            .MapPost(SeenNotifications, "notifications/seen", permission: AuthorizationPolicies.BoqRead)
            .MapGet(GetPdf, "{id:guid}/pdf", permission: AuthorizationPolicies.BoqRead)
            .MapGet(GetById, "{id:guid}", permission: AuthorizationPolicies.BoqRead)
            .MapPost(Create, permission: Permissions.BoqManage)
            .MapPut(Update, "{id:guid}", permission: Permissions.BoqManage)
            // Submitting is the last step of building, so it goes with building.
            .MapPost(Submit, "{id:guid}/submit", permission: Permissions.BoqManage)
            // Answering it does not.
            .MapPost(Approve, "{id:guid}/approve", permission: Permissions.BoqApprove)
            .MapPost(Reject, "{id:guid}/reject", permission: Permissions.BoqApprove)
            .MapDelete(Delete, "{id:guid}", permission: Permissions.BoqManage);
    }

    public async Task<IResult> GetAll(
        ISender sender,
        [AsParameters] GetBoqsQuery query,
        CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(query, cancellationToken));

    public async Task<IResult> GetById(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetBoqQuery(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> Create(
        ISender sender,
        CreateBoqCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/boqs/{result.Data!.Id}", result)
            : Results.BadRequest(result);
    }

    public async Task<IResult> Update(
        ISender sender,
        Guid id,
        UpdateBoqCommand command,
        CancellationToken cancellationToken)
    {
        // The route id wins over anything in the body, so a caller cannot address
        // one BOQ and rewrite another.
        var result = await sender.Send(command with { Id = id }, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> GetNotifications(
        ISender sender,
        [AsParameters] GetBoqNotificationsQuery query,
        CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(query, cancellationToken));

    public async Task<IResult> SeenNotifications(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new SeenBoqNotificationsCommand(), cancellationToken));

    public async Task<IResult> Submit(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SubmitBoqCommand(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Approve(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ApproveBoqCommand(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Reject(
        ISender sender,
        Guid id,
        RejectBoqCommand command,
        CancellationToken cancellationToken)
    {
        // The route id wins over the body, as it does on update.
        var result = await sender.Send(command with { Id = id }, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Delete(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteBoqCommand(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> GetPdf(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetBoqPdfQuery(id), cancellationToken);
        if (!result.Succeeded || result.Data is null)
            return Results.NotFound(ApiResult<string>.Failure(result.Errors));

        return Results.File(result.Data.Content, "application/pdf", result.Data.FileName);
    }
}
