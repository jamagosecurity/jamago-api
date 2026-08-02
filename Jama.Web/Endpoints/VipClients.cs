using Jama.Application.Common;
using Jama.Application.Common.Models;
using Jama.Application.VipClients;
using Jama.Web.Infrastructure;
using MediatR;
using AppRoles = Jama.Application.Common.Roles;

namespace Jama.Web.Endpoints;

/// <summary>
/// VIP client projects and their document folders.
///
/// Management is gated on vip.manage, which admins hold implicitly. The client
/// routes are gated on the Client role and resolve the project from the token,
/// so a client can never name someone else's project.
/// </summary>
public sealed class VipClients : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            // Client portal — own project only.
            .MapGet(GetMyProject, "me", AppRoles.Client)
            // Management.
            .MapGet(GetAll, permission: Permissions.VipManage)
            .MapGet(GetById, "{id:guid}", permission: Permissions.VipManage)
            .MapPost(Create, permission: Permissions.VipManage)
            .MapPut(Update, "{id:guid}", permission: Permissions.VipManage)
            .MapDelete(Delete, "{id:guid}", permission: Permissions.VipManage)
            // Documents. Download is reachable by both audiences, so it requires
            // only authentication here and decides access inside the handler.
            .MapPost(Upload, "folders/{folderId:guid}/documents", permission: Permissions.VipManage, allowFileUpload: true)
            .MapGet(Download, "documents/{documentId:guid}/download", requireAuthorization: true)
            .MapDelete(DeleteDocument, "documents/{documentId:guid}", permission: Permissions.VipManage);
    }

    public async Task<IResult> GetAll(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetVipClientsQuery(), cancellationToken));

    public async Task<IResult> GetById(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetVipClientQuery(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> GetMyProject(ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMyVipProjectQuery(), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> Create(
        ISender sender,
        CreateVipClientCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Update(
        ISender sender,
        Guid id,
        UpdateVipClientCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with { Id = id }, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Delete(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteVipClientCommand(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> Upload(
        ISender sender,
        Guid folderId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return Results.BadRequest(ApiResult<string>.Failure("A file is required."));

        await using var stream = file.OpenReadStream();
        var result = await sender.Send(
            new UploadVipDocumentCommand
            {
                FolderId = folderId,
                FileName = file.FileName,
                ContentType = file.ContentType,
                SizeBytes = file.Length,
                Content = stream,
            },
            cancellationToken);

        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Download(
        ISender sender,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DownloadVipDocumentQuery(documentId), cancellationToken);
        if (!result.Succeeded || result.Data is null)
            return Results.NotFound(ApiResult<string>.Failure(result.Errors));

        // Streamed rather than buffered: a 25 MB upload should not be read into
        // memory just to be written straight back out.
        return Results.File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }

    public async Task<IResult> DeleteDocument(
        ISender sender,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteVipDocumentCommand(documentId), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }
}
