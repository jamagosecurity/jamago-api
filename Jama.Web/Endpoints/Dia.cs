using Jama.Application.Common;
using Jama.Application.Common.Models;
using Jama.Application.Dia;
using Jama.Application.Dia.Commands.ChangeDiaInspectionState;
using Jama.Application.Dia.Commands.CreateDiaInspection;
using Jama.Application.Dia.Commands.DeleteDiaInspection;
using Jama.Application.Dia.Commands.UpdateDiaInspection;
using Jama.Application.Dia.Queries.GetDiaDashboard;
using Jama.Application.Dia.Queries.GetDiaHistory;
using Jama.Application.Dia.Queries.GetDiaInspection;
using Jama.Application.Dia.Queries.GetDiaInspections;
using Jama.Application.Technician;
using Jama.Application.Technician.Queries.GetTechnicianFinalSummary;
using Jama.Web.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Jama.Web.Endpoints;

/// <summary>Administrative DIA inspection lifecycle and reporting endpoints.</summary>
public sealed class Dia : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        // Reads need dia.view, writes need dia.upload — admins satisfy both
        // implicitly. Lifecycle actions (archive/activate/deactivate) stay
        // Admin-only: activating a DIA starts the technician's quarterly clock,
        // which is more than "create and edit records" is meant to allow.
        app.MapGroup(this)
            .MapGet(GetList, permission: Permissions.DiaView)
            .MapGet(GetDashboard, "dashboard", permission: Permissions.DiaView)
            .MapGet(GetInspectionHistory, "inspection-history", permission: Permissions.DiaView)
            .MapGet(GetById, "{id:guid}", permission: Permissions.DiaView)
            .MapGet(GetSubmittedInspections, "{id:guid}/inspections", permission: Permissions.DiaView)
            .MapPost(Create, permission: Permissions.DiaUpload)
            .MapPut(Update, "{id:guid}", permission: Permissions.DiaUpload)
            .MapDelete(Archive, "{id:guid}", Roles.Admin)
            // Permanent delete sits above Admin: archiving is reversible, this is
            // not, so it is held to the single seeded root account.
            .MapDelete(Delete, "{id:guid}/permanent", permission: AuthorizationPolicies.SuperAdmin)
            .MapPost(Activate, "{id:guid}/activate", Roles.Admin)
            .MapPost(Deactivate, "{id:guid}/deactivate", Roles.Admin)
            .MapPost(Restore, "{id:guid}/restore", Roles.Admin);
    }

    public async Task<IResult> GetList(
        ISender sender,
        [AsParameters] GetDiaInspectionsQuery query,
        CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(query, cancellationToken));

    public async Task<IResult> GetById(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetDiaInspectionQuery(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    /// <summary>
    /// What each quarter's inspection actually captured — cameras, network, VMS,
    /// UPS, ANPR and K'Poi. Reuses the technician summary query, which is scoped
    /// by DIA rather than by user, so admins see the same record the technician
    /// submitted rather than a separate reimplementation of it.
    /// </summary>
    public async Task<IResult> GetSubmittedInspections(
        ISender sender,
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTechnicianFinalSummaryQuery(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }

    public async Task<IResult> Create(
        ISender sender,
        CreateDiaInspectionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        if (!result.Succeeded)
            return Results.Conflict(result);
        return Results.Created($"/api/dia/{result.Data!.Id}", result);
    }

    public async Task<IResult> Update(
        ISender sender,
        Guid id,
        UpdateDiaInspectionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with { Id = id }, cancellationToken);
        if (result.Succeeded) return Results.Ok(result);
        return IsNotFound(result.Errors) ? Results.NotFound(result) : Results.Conflict(result);
    }

    public async Task<IResult> Archive(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ChangeDiaInspectionStateCommand(id, DiaMutation.Archive), cancellationToken);
        return result.Succeeded ? Results.NoContent() : Results.NotFound(result);
    }

    public async Task<IResult> Delete(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteDiaInspectionCommand(id), cancellationToken);
        if (result.Succeeded)
        {
            return Results.NoContent();
        }

        // A refusal here means the record exists but is protected — not archived,
        // or carrying inspections. That is a conflict, not a missing resource, and
        // the UI shows the reason to the operator.
        return result.Errors.Contains(DeleteDiaInspectionCommand.NotFoundError)
            ? Results.NotFound(result)
            : Results.Conflict(result);
    }

    public Task<IResult> Activate(ISender sender, Guid id, CancellationToken cancellationToken) =>
        ChangeState(sender, id, DiaMutation.Activate, cancellationToken);

    public Task<IResult> Deactivate(ISender sender, Guid id, CancellationToken cancellationToken) =>
        ChangeState(sender, id, DiaMutation.Deactivate, cancellationToken);

    /// <summary>Brings an archived DIA back into the register, inactive.</summary>
    public Task<IResult> Restore(ISender sender, Guid id, CancellationToken cancellationToken) =>
        ChangeState(sender, id, DiaMutation.Restore, cancellationToken);

    public async Task<IResult> GetDashboard(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetDiaDashboardQuery(), cancellationToken));

    public async Task<IResult> GetInspectionHistory(
        ISender sender,
        [AsParameters] GetDiaHistoryQuery query,
        CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(query, cancellationToken));

    private static async Task<IResult> ChangeState(
        ISender sender,
        Guid id,
        DiaMutation mutation,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ChangeDiaInspectionStateCommand(id, mutation), cancellationToken);
        if (result.Succeeded) return Results.Ok(result);
        if (IsNotFound(result.Errors)) return Results.NotFound(result);
        return Results.Conflict(result);
    }

    private static bool IsNotFound(IEnumerable<string> errors) =>
        errors.Any(x => x.Contains("not found", StringComparison.OrdinalIgnoreCase));
}
