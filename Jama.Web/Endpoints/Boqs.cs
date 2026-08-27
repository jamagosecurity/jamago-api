using Jama.Application.Boqs.Commands.CreateBoq;
using Jama.Application.Boqs.Commands.DeleteBoq;
using Jama.Application.Boqs.Commands.UpdateBoq;
using Jama.Application.Boqs.Queries.GetBoq;
using Jama.Application.Boqs.Queries.GetBoqPdf;
using Jama.Application.Boqs.Queries.GetBoqs;
using Jama.Application.Common;
using Jama.Application.Common.Models;
using Jama.Web.Infrastructure;
using MediatR;

namespace Jama.Web.Endpoints;

/// <summary>
/// Bills of quantities, assembled from the stock catalogue.
///
/// Every route requires boq.manage — admins hold it implicitly and can grant it
/// to individual staff, which is how a staff account gets the BOQ screens in its
/// portal.
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
            .MapGet(GetAll, permission: Permissions.BoqManage)
            .MapGet(GetPdf, "{id:guid}/pdf", permission: Permissions.BoqManage)
            .MapGet(GetById, "{id:guid}", permission: Permissions.BoqManage)
            .MapPost(Create, permission: Permissions.BoqManage)
            .MapPut(Update, "{id:guid}", permission: Permissions.BoqManage)
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
