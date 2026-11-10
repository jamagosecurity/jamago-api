using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.NonMoiJobs.Queries;

/// <summary>Every job, newest first, for admin. Small enough to filter in the page.</summary>
public sealed record GetNonMoiJobsQuery : IRequest<ApiResult<IReadOnlyList<NonMoiJobDto>>>;

public sealed class GetNonMoiJobsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetNonMoiJobsQuery, ApiResult<IReadOnlyList<NonMoiJobDto>>>
{
    public async Task<ApiResult<IReadOnlyList<NonMoiJobDto>>> Handle(
        GetNonMoiJobsQuery request, CancellationToken cancellationToken)
    {
        var jobs = await context.NonMoiJobs.AsNoTracking().Include(x => x.Lines).Include(x => x.Events)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return ApiResult<IReadOnlyList<NonMoiJobDto>>.Success(jobs.Select(NonMoiJobRules.ToDto).ToList());
    }
}

/// <summary>How many finished jobs are waiting for the signed-in person to close: the ones they issued, or every one
/// for an administrator. Drives the red number beside Non-MOI Jobs in the sidebar.</summary>
public sealed record GetJobsAwaitingCloseQuery : IRequest<ApiResult<int>>;

public sealed class GetJobsAwaitingCloseQueryHandler(IApplicationDbContext context, ICurrentUser actor)
    : IRequestHandler<GetJobsAwaitingCloseQuery, ApiResult<int>>
{
    public async Task<ApiResult<int>> Handle(GetJobsAwaitingCloseQuery request, CancellationToken cancellationToken)
    {
        // Finished and waiting for the issuer to send, or sent and the client asked for changes.
        var waiting = context.NonMoiJobs.AsNoTracking().Where(x => x.Status == "Completed" || x.Status == "ChangesRequested");
        if (actor.Role != Jama.Application.Common.Roles.Admin)
            waiting = waiting.Where(x => x.AssignedById == actor.UserId);
        return ApiResult<int>.Success(await waiting.CountAsync(cancellationToken));
    }
}

/// <summary>The signed-in technician's own jobs — only those assigned to them.</summary>
public sealed record GetMyNonMoiJobsQuery : IRequest<ApiResult<IReadOnlyList<NonMoiJobDto>>>;

public sealed class GetMyNonMoiJobsQueryHandler(IApplicationDbContext context, ICurrentUser actor)
    : IRequestHandler<GetMyNonMoiJobsQuery, ApiResult<IReadOnlyList<NonMoiJobDto>>>
{
    public async Task<ApiResult<IReadOnlyList<NonMoiJobDto>>> Handle(
        GetMyNonMoiJobsQuery request, CancellationToken cancellationToken)
    {
        var jobs = await context.NonMoiJobs.AsNoTracking().Include(x => x.Lines).Include(x => x.Events)
            .Where(x => x.AssignedToId == actor.UserId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return ApiResult<IReadOnlyList<NonMoiJobDto>>.Success(jobs.Select(NonMoiJobRules.ToDto).ToList());
    }
}

/// <summary>Who a job can be issued to — the picker's own list, queried here so
/// a maintenance.manage holder who is not an administrator can read it.</summary>
public sealed record GetJobTechniciansQuery : IRequest<ApiResult<IReadOnlyList<JobTechnicianDto>>>;

public sealed record JobTechnicianDto(Guid AdminUserId, string FullName);

public sealed class GetJobTechniciansQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetJobTechniciansQuery, ApiResult<IReadOnlyList<JobTechnicianDto>>>
{
    public async Task<ApiResult<IReadOnlyList<JobTechnicianDto>>> Handle(
        GetJobTechniciansQuery request, CancellationToken cancellationToken)
    {
        var technicians = await NonMoiJobRules.Technicians(context)
            .OrderBy(s => s.FullName)
            .Select(s => new JobTechnicianDto(s.AdminUserId!.Value, s.FullName))
            .ToListAsync(cancellationToken);
        return ApiResult<IReadOnlyList<JobTechnicianDto>>.Success(technicians);
    }
}
