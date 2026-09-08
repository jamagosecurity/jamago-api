using Jama.Application.Common;
using Jama.Application.Common.Models;
using Jama.Application.Drawings.Commands.ApproveDrawing;
using Jama.Application.Drawings.Commands.CreateDrawing;
using Jama.Application.Drawings.Commands.DeleteDrawing;
using Jama.Application.Drawings.Commands.DeleteDrawingFile;
using Jama.Application.Drawings.Commands.RejectDrawing;
using Jama.Application.Drawings.Commands.SubmitDrawing;
using Jama.Application.Drawings.Commands.UpdateDrawing;
using Jama.Application.Drawings.Commands.UploadDrawingFile;
using Jama.Application.Drawings.Queries.GetDrawing;
using Jama.Application.Drawings.Queries.GetDrawingFile;
using Jama.Application.Drawings.Queries.GetDrawings;
using Jama.Web.Infrastructure;
using MediatR;

namespace Jama.Web.Endpoints;

/// <summary>
/// CAD drawings, submitted for approval independently of quotations.
///
/// Two grants, mirroring boq.manage/boq.approve. drawing.manage drafts and
/// submits; drawing.approve answers. Reads accept either, because an approver
/// has to open the drawing to decide on it — see AuthorizationPolicies.DrawingRead.
/// The split is enforced here, not by which buttons the client draws: a request
/// straight to /approve from an account without the grant is refused by the
/// policy regardless of what screen it came from.
///
/// A drawing's content is its files rather than nested lines, so there is no
/// equivalent of BoqWriter here — see UploadDrawingFile for what a submission
/// actually requires (a DWG or DXF is not enough on its own; a plotted PDF has
/// to be attached too, since neither renders in a browser).
/// </summary>
public sealed class Drawings : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapGet(GetAll, permission: AuthorizationPolicies.DrawingRead)
            .MapGet(GetById, "{id:guid}", permission: AuthorizationPolicies.DrawingRead)
            .MapGet(DownloadFile, "files/{fileId:guid}", permission: AuthorizationPolicies.DrawingRead)
            .MapPost(Create, permission: Permissions.DrawingManage)
            .MapPut(Update, "{id:guid}", permission: Permissions.DrawingManage)
            .MapPost(UploadFile, "{id:guid}/files", permission: Permissions.DrawingManage, allowFileUpload: true)
            .MapDelete(DeleteFile, "files/{fileId:guid}", permission: Permissions.DrawingManage)
            // Submitting is the last step of drafting, so it goes with drafting.
            .MapPost(Submit, "{id:guid}/submit", permission: Permissions.DrawingManage)
            // Answering it does not.
            .MapPost(Approve, "{id:guid}/approve", permission: Permissions.DrawingApprove)
            .MapPost(Reject, "{id:guid}/reject", permission: Permissions.DrawingApprove)
            .MapDelete(Delete, "{id:guid}", permission: Permissions.DrawingManage);
    }

    public async Task<IResult> GetAll(
        ISender sender,
        [AsParameters] GetDrawingsQuery query,
        CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(query, cancellationToken));

    public async Task<IResult> GetById(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetDrawingQuery(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> Create(
        ISender sender,
        CreateDrawingCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/drawings/{result.Data!.Id}", result)
            : Results.BadRequest(result);
    }

    public async Task<IResult> Update(
        ISender sender,
        Guid id,
        UpdateDrawingCommand command,
        CancellationToken cancellationToken)
    {
        // The route id wins over anything in the body, so a caller cannot
        // address one drawing and rewrite another.
        var result = await sender.Send(command with { Id = id }, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> UploadFile(
        ISender sender,
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return Results.BadRequest(ApiResult<string>.Failure("A file is required."));

        await using var stream = file.OpenReadStream();
        var result = await sender.Send(
            new UploadDrawingFileCommand
            {
                DrawingId = id,
                FileName = file.FileName,
                ContentType = file.ContentType,
                SizeBytes = file.Length,
                Content = stream,
            },
            cancellationToken);

        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> DownloadFile(ISender sender, Guid fileId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetDrawingFileQuery(fileId), cancellationToken);
        if (!result.Succeeded || result.Data is null)
            return Results.NotFound(ApiResult<string>.Failure(result.Errors));

        // Streamed rather than buffered: a 150 MB drawing should not be read
        // into memory just to be written straight back out.
        return Results.File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }

    public async Task<IResult> DeleteFile(ISender sender, Guid fileId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteDrawingFileCommand(fileId), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Submit(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SubmitDrawingCommand(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Approve(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ApproveDrawingCommand(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Reject(
        ISender sender,
        Guid id,
        RejectDrawingCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with { Id = id }, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Delete(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteDrawingCommand(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }
}
