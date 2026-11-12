using Jama.Application.Profiles;
using Jama.Web.Infrastructure;
using MediatR;

namespace Jama.Web.Endpoints;

/// <summary>
/// A person's own profile: the picture and the designation they write. Each account edits its own;
/// an administrator may edit anyone's (the handlers decide). Failures are 400s with a message, never 403,
/// so a refused edit shows its reason instead of bouncing the screen.
/// </summary>
public sealed class Profile : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapPut(Update, "{id:guid}", requireAuthorization: true)
            .MapPost(UploadPhoto, "{id:guid}/photo", requireAuthorization: true, allowFileUpload: true)
            .MapDelete(RemovePhoto, "{id:guid}/photo", requireAuthorization: true)
            .MapGet(GetPhoto, "{id:guid}/photo", requireAuthorization: true);
    }

    public sealed record UpdateProfileBody(string? Designation, string? Phone);

    public async Task<IResult> Update(ISender sender, Guid id, UpdateProfileBody body, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateProfileCommand(id, body.Designation, body.Phone), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> UploadPhoto(ISender sender, Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return Results.BadRequest(Jama.Application.Common.Models.ApiResult<string>.Failure("A picture is required."));

        await using var stream = file.OpenReadStream();
        var result = await sender.Send(
            new SetProfilePhotoCommand { UserId = id, FileName = file.FileName, ContentType = file.ContentType, SizeBytes = file.Length, Content = stream },
            cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> RemovePhoto(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RemoveProfilePhotoCommand(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> GetPhoto(ISender sender, HttpContext http, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetProfilePhotoQuery(id), cancellationToken);
        if (!result.Succeeded || result.Data is null) return Results.NotFound();

        // The page asks for ?v=<version>, so a new picture is a new URL and this can be cached hard.
        http.Response.Headers.CacheControl = "private, max-age=604800";
        return Results.File(result.Data.Content, result.Data.ContentType);
    }
}
