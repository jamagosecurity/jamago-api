using Jama.Application.Common;
using Jama.Application.StorageDesigns.Queries.CalculateStorageDesign;
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
            .MapPost(Calculate, "calculate", permission: Permissions.BoqManage);
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
