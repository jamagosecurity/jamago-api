using Jama.Application.Common;
using Jama.Application.Technician;
using Jama.Web.Infrastructure;
using MediatR;

namespace Jama.Web.Endpoints;

/// <summary>Admin invoice listing, PDF download and anonymous share-link access.</summary>
public sealed class Invoices : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapGet(GetList, roles: Roles.Admin)
            .MapGet(Download, "{id:guid}/download", Roles.Admin)
            .MapPost(Share, "{id:guid}/share", Roles.Admin)
            .MapGet(DownloadShared, "shared/{token}");
    }

    public async Task<IResult> GetList(
        ISender sender,
        [AsParameters] GetTechnicianInvoicesQuery query,
        CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(query, cancellationToken));

    public async Task<IResult> Download(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetInvoicePdfQuery(id), cancellationToken);
        if (!result.Succeeded || result.Data is null)
            return Results.NotFound(result);
        return Results.File(result.Data.Content, "application/pdf", result.Data.FileName);
    }

    public async Task<IResult> Share(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateInvoiceShareLinkCommand(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> DownloadShared(ISender sender, string token, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSharedInvoicePdfQuery(token), cancellationToken);
        if (!result.Succeeded || result.Data is null)
            return Results.NotFound(result);
        return Results.File(result.Data.Content, "application/pdf", result.Data.FileName);
    }
}
