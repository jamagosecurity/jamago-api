using Jama.Application.Common;
using Jama.Application.Common.Models;
using Jama.Application.Dia;
using Jama.Web.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Jama.Web.Endpoints;

/// <summary>Administrative DIA inspection lifecycle and reporting endpoints.</summary>
public sealed class Dia : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapGet(GetList, roles: Roles.Admin)
            .MapGet(GetDashboard, "dashboard", Roles.Admin)
            .MapGet(GetInspectionHistory, "inspection-history", Roles.Admin)
            .MapGet(GetById, "{id:guid}", Roles.Admin)
            .MapPost(Create, roles: Roles.Admin)
            .MapPut(Update, "{id:guid}", Roles.Admin)
            .MapDelete(Archive, "{id:guid}", Roles.Admin)
            .MapPost(Activate, "{id:guid}/activate", Roles.Admin)
            .MapPost(Deactivate, "{id:guid}/deactivate", Roles.Admin);
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

    public Task<IResult> Activate(ISender sender, Guid id, CancellationToken cancellationToken) =>
        ChangeState(sender, id, DiaMutation.Activate, cancellationToken);

    public Task<IResult> Deactivate(ISender sender, Guid id, CancellationToken cancellationToken) =>
        ChangeState(sender, id, DiaMutation.Deactivate, cancellationToken);

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
