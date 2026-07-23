using AppRoles = Jama.Application.Common.Roles;
using Jama.Application.Common.Models;
using Jama.Application.Technician;
using Jama.Web.Infrastructure;
using MediatR;

namespace Jama.Web.Endpoints;

/// <summary>Technician inspection workflow for activated DIA records.</summary>
public sealed class Technician : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapGet(GetDiaList, "dia", AppRoles.Technician)
            .MapGet(GetInspectionById, "inspection/{id:guid}", AppRoles.Technician)
            .MapGet(GetDiaById, "dia/{id:guid}", AppRoles.Technician)
            .MapGet(GetDiaSummary, "dia/{id:guid}/summary", AppRoles.Technician)
            .MapPost(StartInspection, "start", AppRoles.Technician)
            .MapPost(SaveDraft, "save-draft", AppRoles.Technician)
            .MapPost(SubmitInspection, "submit", AppRoles.Technician)
            .MapGet(GetHistory, "history", AppRoles.Technician)
            .MapGet(GetInvoices, "invoices", AppRoles.Technician)
            .MapPost(ReopenInspection, "{inspectionId:guid}/reopen", AppRoles.Admin);
    }

    public async Task<IResult> GetDiaList(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetTechnicianDiaListQuery(), cancellationToken));

    public async Task<IResult> GetInspectionById(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTechnicianInspectionQuery(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> GetDiaById(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTechnicianDiaQuery(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> GetDiaSummary(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTechnicianFinalSummaryQuery(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> StartInspection(
        ISender sender,
        StartTechnicianInspectionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        if (result.Succeeded) return Results.Ok(result);
        return IsConflict(result.Errors) ? Results.Conflict(result) : Results.BadRequest(result);
    }

    public async Task<IResult> SaveDraft(
        ISender sender,
        SaveTechnicianInspectionDraftCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        if (result.Succeeded) return Results.Ok(result);
        return IsConflict(result.Errors) ? Results.Conflict(result) : Results.BadRequest(result);
    }

    public async Task<IResult> SubmitInspection(
        ISender sender,
        SubmitTechnicianInspectionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        if (result.Succeeded) return Results.Ok(result);
        return IsConflict(result.Errors) ? Results.Conflict(result) : Results.BadRequest(result);
    }

    public async Task<IResult> GetHistory(
        ISender sender,
        [AsParameters] GetTechnicianHistoryQuery query,
        CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(query, cancellationToken));

    public async Task<IResult> GetInvoices(
        ISender sender,
        [AsParameters] GetTechnicianInvoicesQuery query,
        CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(query, cancellationToken));

    public async Task<IResult> ReopenInspection(
        ISender sender,
        Guid inspectionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ReopenTechnicianInspectionCommand(inspectionId), cancellationToken);
        if (result.Succeeded) return Results.Ok(result);
        return IsConflict(result.Errors) ? Results.Conflict(result) : Results.NotFound(result);
    }

    private static bool IsConflict(IEnumerable<string> errors) =>
        errors.Any(x => x.Contains("read only", StringComparison.OrdinalIgnoreCase)
            || x.Contains("already submitted", StringComparison.OrdinalIgnoreCase)
            || x.Contains("another session", StringComparison.OrdinalIgnoreCase));
}
