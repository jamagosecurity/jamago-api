using Jama.Application.Common;
using Jama.Application.Common.Models;
using Jama.Application.Maintenance;
using Jama.Application.Maintenance.Commands.AssignMaintenanceRequest;
using Jama.Application.Maintenance.Commands.CancelMaintenanceRequest;
using Jama.Application.Maintenance.Commands.CreateMaintenanceCatalogueItem;
using Jama.Application.Maintenance.Commands.CreateMaintenanceRequest;
using Jama.Application.Maintenance.Commands.DeleteMaintenanceCatalogueImage;
using Jama.Application.Maintenance.Commands.DeleteMaintenanceCategory;
using Jama.Application.Maintenance.Commands.DeleteMaintenanceCatalogueItem;
using Jama.Application.Maintenance.Commands.DeleteMaintenanceRequest;
using Jama.Application.Maintenance.Commands.RenameMaintenanceCategory;
using Jama.Application.Maintenance.Commands.ResolveMaintenanceRequest;
using Jama.Application.Maintenance.Commands.UpdateMaintenanceCatalogueItem;
using Jama.Application.Maintenance.Commands.UpdateMaintenanceRequest;
using Jama.Application.Maintenance.Commands.UploadMaintenanceCatalogueImage;
using Jama.Application.Maintenance.Queries.GetAssignableStaff;
using Jama.Application.Maintenance.Queries.GetMaintenanceCatalogue;
using Jama.Application.Maintenance.Queries.GetMaintenanceCatalogueImage;
using Jama.Application.Maintenance.Queries.GetMaintenanceRequest;
using Jama.Application.Maintenance.Queries.GetMaintenanceRequests;
using Jama.Application.Maintenance.Queries.GetMyMaintenanceRequests;
using Jama.Web.Infrastructure;
using MediatR;

namespace Jama.Web.Endpoints;

/// <summary>
/// Paid service callouts — power, cabling, camera, NVR, and software trouble
/// at a site. Non-MOI: no relation to the regulated DIA inspection cycle.
///
/// Filing a ticket needs only authentication — a client reporting their own
/// issue, or staff logging one on someone's behalf. Everything past that
/// (the company-wide queue, assigning, resolving, cancelling, editing) is
/// gated on maintenance.manage. Reading one ticket by id is open to anyone
/// authenticated; the handler decides whether THIS caller may see THIS row —
/// see MaintenanceVisibility.
/// </summary>
public sealed class Maintenance : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapPost(Create, requireAuthorization: true)
            .MapGet(GetAll, permission: Permissions.MaintenanceManage)
            .MapGet(GetMine, "mine", requireAuthorization: true)
            .MapGet(GetById, "{id:guid}", requireAuthorization: true)
            .MapPut(Update, "{id:guid}", permission: Permissions.MaintenanceManage)
            .MapPost(Assign, "{id:guid}/assign", permission: Permissions.MaintenanceManage)
            .MapPost(Resolve, "{id:guid}/resolve", permission: Permissions.MaintenanceManage)
            .MapPost(Cancel, "{id:guid}/cancel", permission: Permissions.MaintenanceManage)
            .MapDelete(Delete, "{id:guid}", permission: Permissions.MaintenanceManage)
            .MapGet(IssueCatalogue, "issue-catalogue", requireAuthorization: true)
            // Served without a token so an <img src> can show it, as a stock
            // item's pictures are; the ids are unguessable guids.
            .MapGet(GetCatalogueImage, "issue-catalogue/images/{imageId:guid}")
            .MapPost(UploadCatalogueImage, "issue-catalogue/{id:guid}/images", permission: Permissions.MaintenanceManage, allowFileUpload: true)
            .MapDelete(DeleteCatalogueImage, "issue-catalogue/images/{imageId:guid}", permission: Permissions.MaintenanceManage)
            .MapPost(CreateCatalogueItem, "issue-catalogue", permission: Permissions.MaintenanceManage)
            .MapPut(UpdateCatalogueItem, "issue-catalogue/{id:guid}", permission: Permissions.MaintenanceManage)
            .MapDelete(DeleteCatalogueItem, "issue-catalogue/{id:guid}", permission: Permissions.MaintenanceManage)
            .MapPost(RenameCategory, "issue-catalogue/categories/rename", permission: Permissions.MaintenanceManage)
            .MapDelete(DeleteCategory, "issue-catalogue/categories", permission: Permissions.MaintenanceManage)
            .MapGet(AssignableStaff, "assignable-staff", permission: Permissions.MaintenanceManage);
    }

    public async Task<IResult> Create(
        ISender sender, CreateMaintenanceRequestCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/maintenance/{result.Data!.Id}", result)
            : Results.BadRequest(result);
    }

    public async Task<IResult> GetAll(
        ISender sender, [AsParameters] GetMaintenanceRequestsQuery query, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(query, cancellationToken));

    public async Task<IResult> GetMine(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetMyMaintenanceRequestsQuery(), cancellationToken));

    public async Task<IResult> GetById(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMaintenanceRequestQuery(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> Update(
        ISender sender, Guid id, UpdateMaintenanceRequestCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with { Id = id }, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Assign(
        ISender sender, Guid id, AssignRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AssignMaintenanceRequestCommand(id, request.StaffId), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Resolve(
        ISender sender, Guid id, ResolveRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ResolveMaintenanceRequestCommand(id, request.Notes), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Cancel(
        ISender sender, Guid id, CancelRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CancelMaintenanceRequestCommand(id, request.Reason), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Delete(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteMaintenanceRequestCommand(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> IssueCatalogue(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetMaintenanceCatalogueQuery(), cancellationToken));

    public async Task<IResult> CreateCatalogueItem(
        ISender sender, CreateMaintenanceCatalogueItemCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> UpdateCatalogueItem(
        ISender sender, Guid id, UpdateMaintenanceCatalogueItemCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with { Id = id }, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> UploadCatalogueImage(
        ISender sender, Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return Results.BadRequest(ApiResult<string>.Failure("An image is required."));

        await using var stream = file.OpenReadStream();
        var result = await sender.Send(
            new UploadMaintenanceCatalogueImageCommand
            {
                ItemId = id,
                FileName = file.FileName,
                ContentType = file.ContentType,
                SizeBytes = file.Length,
                Content = stream,
            },
            cancellationToken);

        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> GetCatalogueImage(ISender sender, Guid imageId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMaintenanceCatalogueImageQuery(imageId), cancellationToken);
        if (!result.Succeeded || result.Data is null)
            return Results.NotFound(ApiResult<string>.Failure(result.Errors));

        // Streamed and served inline so an <img src> shows it.
        return Results.File(result.Data.Content, result.Data.ContentType);
    }

    public async Task<IResult> DeleteCatalogueImage(ISender sender, Guid imageId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteMaintenanceCatalogueImageCommand(imageId), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> RenameCategory(
        ISender sender, RenameMaintenanceCategoryCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    // The name travels in the query string: categories are free text and may
    // hold characters (a slash, say) that do not belong in a route segment.
    public async Task<IResult> DeleteCategory(ISender sender, string name, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteMaintenanceCategoryCommand(name), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> DeleteCatalogueItem(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteMaintenanceCatalogueItemCommand(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> AssignableStaff(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetAssignableStaffQuery(), cancellationToken));
}

public sealed record AssignRequest(Guid StaffId);
public sealed record ResolveRequest(string Notes);
public sealed record CancelRequest(string Reason);
