using Jama.Application.Common.Models;
using Jama.Application.Quotations.Commands.CreateQuotation;
using Jama.Application.Quotations.Commands.DeleteQuotation;
using Jama.Application.Quotations.Commands.UpdateQuotation;
using Jama.Application.Quotations.Queries.GetQuotation;
using Jama.Application.Quotations.Queries.GetQuotationPdf;
using Jama.Application.Quotations.Queries.GetQuotations;
using Jama.Application.Quotations.Queries.GetQuotationSummary;
using Jama.Web.Infrastructure;
using MediatR;

namespace Jama.Web.Endpoints;

/// <summary>
/// Customer quotations, priced from the stock inventory.
///
/// DELIBERATELY ANONYMOUS, matching the inventory it is built from — asked for
/// so the panel can be used without a login.
///
/// This one carries more than the stock list does: customer names, contact
/// details and the prices offered to them. Anyone who can reach the host can
/// read, edit and delete quotations and download their PDFs, with no record of
/// who did it. That is a bigger exposure than a catalogue of camera models, and
/// worth closing before this reaches a public host.
/// </summary>
public sealed class Quotations : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        // The literal segments are safe alongside the {id:guid} routes because
        // "summary" cannot satisfy a guid constraint.
        app.MapGroup(this)
            .MapGet(GetAll)
            .MapGet(GetSummary, "summary")
            .MapGet(GetPdf, "{id:guid}/pdf")
            .MapGet(GetById, "{id:guid}")
            .MapPost(Create)
            .MapPut(Update, "{id:guid}")
            .MapDelete(Delete, "{id:guid}");
    }

    public async Task<IResult> GetAll(
        ISender sender,
        [AsParameters] GetQuotationsQuery query,
        CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(query, cancellationToken));

    public async Task<IResult> GetSummary(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetQuotationSummaryQuery(), cancellationToken));

    public async Task<IResult> GetById(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetQuotationQuery(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> Create(
        ISender sender,
        CreateQuotationCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/quotations/{result.Data!.Id}", result)
            : Results.BadRequest(result);
    }

    public async Task<IResult> Update(
        ISender sender,
        Guid id,
        UpdateQuotationCommand command,
        CancellationToken cancellationToken)
    {
        // The route id wins over anything in the body, so a caller cannot address
        // one quotation and rewrite another.
        var result = await sender.Send(command with { Id = id }, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Delete(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteQuotationCommand(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> GetPdf(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetQuotationPdfQuery(id), cancellationToken);
        if (!result.Succeeded || result.Data is null)
            return Results.NotFound(ApiResult<string>.Failure(result.Errors));

        return Results.File(result.Data.Content, "application/pdf", result.Data.FileName);
    }
}
