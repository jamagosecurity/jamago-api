using Jama.Application.Common;
using Jama.Application.Common.Models;
using Jama.Application.StorageDesigns.Queries.CalculateStorageDesign;
using Jama.Application.StorageDesigns.Queries.GetStorageDesignPdf;
using Jama.Web.Infrastructure;
using MediatR;

namespace Jama.Web.Endpoints;

/// <summary>
/// CCTV storage sizing.
///
/// One stateless route. A design is a calculation over a quotation's cameras,
/// not a record of its own: storing one would go stale the moment a line changed,
/// and the caller can always recompute in a millisecond.
///
/// Gated on boq.manage — sizing an array is part of quoting for it, so anyone
/// who can build a quotation can size its storage. Admins hold it implicitly.
///
/// POST rather than GET despite reading nothing: the input is a nested document
/// of camera, ANPR and disk-group arrays, which does not survive a query string.
/// </summary>
public sealed class StorageDesigns : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapPost(Calculate, "calculate", permission: Permissions.BoqManage)
            .MapPost(Pdf, "pdf", permission: Permissions.BoqManage);
    }

    /// <summary>
    /// The MOI submission sheet as a PDF. POST for the same reason Calculate is:
    /// the input is a nested document that will not survive a query string.
    /// </summary>
    public async Task<IResult> Pdf(
        ISender sender,
        GetStorageDesignPdfQuery query,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(query, cancellationToken);
        if (!result.Succeeded || result.Data is null)
            return Results.BadRequest(ApiResult<string>.Failure(result.Errors));

        return Results.File(result.Data.Content, "application/pdf", result.Data.FileName);
    }

    public async Task<IResult> Calculate(
        ISender sender,
        CalculateStorageDesignQuery query,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(query, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }
}
