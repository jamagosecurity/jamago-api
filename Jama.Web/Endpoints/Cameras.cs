using Jama.Application.Cameras.Commands.CreateCamera;
using Jama.Application.Cameras.Commands.DeleteCamera;
using Jama.Application.Cameras.Commands.DeleteCameraImage;
using Jama.Application.Cameras.Commands.UpdateCamera;
using Jama.Application.Cameras.Commands.UploadCameraImage;
using Jama.Application.Cameras.Queries.GetCamera;
using Jama.Application.Cameras.Queries.GetCameraImage;
using Jama.Application.Cameras.Queries.GetCameras;
using Jama.Application.Cameras.Queries.GetCameraSummary;
using Jama.Application.Cameras.Queries.GetCameraBrands;
using Jama.Application.Cameras.Queries.GetCameraTypeCounts;
using Jama.Application.Common;
using Jama.Application.Common.Models;
using Jama.Web.Infrastructure;
using MediatR;

namespace Jama.Web.Endpoints;

/// <summary>
/// The stock inventory: one line per brand, type and model, with pricing,
/// warranty and product photos.
///
/// Split down the middle by design:
///
/// READS are anonymous. The public catalogue lists what is in stock and what it
/// costs, and needs no login to browse.
///
/// WRITES require camera.manage. Prices are set once, in the admin panel, by
/// someone who is allowed to set them — a visitor reading the catalogue has no
/// way to change what anything costs. Admins hold the permission implicitly and
/// can grant it to individual staff.
/// </summary>
public sealed class Cameras : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        // Reads carry no protection so the public catalogue can browse them;
        // every write names the permission.
        //
        // The literal segments are safe alongside the {id:guid} routes because
        // neither "summary", "types" nor "images" can satisfy a guid constraint.
        app.MapGroup(this)
            .MapGet(GetAll)
            .MapGet(GetSummary, "summary")
            .MapGet(GetTypeCounts, "types")
            .MapGet(GetBrands, "brands")
            .MapGet(GetImage, "images/{imageId:guid}")
            .MapGet(GetById, "{id:guid}")
            .MapPost(Create, permission: Permissions.CameraManage)
            .MapPost(UploadImage, "{id:guid}/images", permission: Permissions.CameraManage, allowFileUpload: true)
            .MapPut(Update, "{id:guid}", permission: Permissions.CameraManage)
            .MapDelete(DeleteImage, "images/{imageId:guid}", permission: Permissions.CameraManage)
            .MapDelete(Delete, "{id:guid}", permission: Permissions.CameraManage);
    }

    public async Task<IResult> GetAll(
        ISender sender,
        [AsParameters] GetCamerasQuery query,
        CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(query, cancellationToken));

    public async Task<IResult> GetSummary(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetCameraSummaryQuery(), cancellationToken));

    public async Task<IResult> GetTypeCounts(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetCameraTypeCountsQuery(), cancellationToken));

    public async Task<IResult> GetBrands(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetCameraBrandsQuery(), cancellationToken));

    public async Task<IResult> GetById(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCameraQuery(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> Create(
        ISender sender,
        CreateCameraCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/cameras/{result.Data!.Id}", result)
            : Results.BadRequest(result);
    }

    public async Task<IResult> Update(
        ISender sender,
        Guid id,
        UpdateCameraCommand command,
        CancellationToken cancellationToken)
    {
        // The route id wins over anything in the body, so a caller cannot address
        // one row and rewrite another.
        var result = await sender.Send(command with { Id = id }, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Delete(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteCameraCommand(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> UploadImage(
        ISender sender,
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return Results.BadRequest(ApiResult<string>.Failure("An image is required."));

        await using var stream = file.OpenReadStream();
        var result = await sender.Send(
            new UploadCameraImageCommand
            {
                CameraId = id,
                FileName = file.FileName,
                ContentType = file.ContentType,
                SizeBytes = file.Length,
                Content = stream,
            },
            cancellationToken);

        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> GetImage(
        ISender sender,
        Guid imageId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCameraImageQuery(imageId), cancellationToken);
        if (!result.Succeeded || result.Data is null)
            return Results.NotFound(ApiResult<string>.Failure(result.Errors));

        // Streamed rather than buffered, and served inline so an <img src> shows
        // it instead of the browser offering a download.
        return Results.File(result.Data.Content, result.Data.ContentType);
    }

    public async Task<IResult> DeleteImage(
        ISender sender,
        Guid imageId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteCameraImageCommand(imageId), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }
}
