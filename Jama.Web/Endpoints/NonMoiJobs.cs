using Jama.Application.Common;
using Jama.Application.NonMoiJobs.Commands;
using Jama.Application.NonMoiJobs.Queries;
using Jama.Web.Infrastructure;
using MediatR;
using AppRoles = Jama.Application.Common.Roles;

namespace Jama.Web.Endpoints;

/// <summary>
/// Non-MOI jobs admin issues to a technician. Managing them takes
/// nonmoijob.manage (which maintenance.manage implies); a technician reads
/// only the jobs assigned to them.
/// </summary>
public sealed record CompleteJobBody(IReadOnlyList<CompleteJobLine>? Lines, string? Notes);

public sealed class NonMoiJobs : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapGet(GetAll, permission: Permissions.NonMoiJobManage)
            .MapGet(GetMine, "mine", AppRoles.Technician)
            .MapPost(Complete, "{id:guid}/complete", AppRoles.Technician)
            .MapGet(GetTechnicians, "technicians", permission: Permissions.NonMoiJobManage)
            .MapPost(Create, permission: Permissions.NonMoiJobManage)
            .MapPut(Update, "{id:guid}", permission: Permissions.NonMoiJobManage)
            .MapDelete(Delete, "{id:guid}", permission: Permissions.NonMoiJobManage);
    }

    public async Task<IResult> GetAll(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetNonMoiJobsQuery(), cancellationToken));

    public async Task<IResult> GetMine(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetMyNonMoiJobsQuery(), cancellationToken));

    public async Task<IResult> GetTechnicians(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetJobTechniciansQuery(), cancellationToken));

    public async Task<IResult> Complete(
        ISender sender, Guid id, CompleteJobBody body, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CompleteNonMoiJobCommand(id, body.Lines, body.Notes), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Create(ISender sender, CreateNonMoiJobCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Update(
        ISender sender, Guid id, UpdateNonMoiJobCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with { Id = id }, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    public async Task<IResult> Delete(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteNonMoiJobCommand(id), cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.NotFound(result);
    }
}
