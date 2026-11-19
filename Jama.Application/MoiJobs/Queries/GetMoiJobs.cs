using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.MoiJobs.Queries;

/// <summary>Every job, newest first, for admin. Small enough to filter in the page.</summary>
public sealed record GetMoiJobsQuery : IRequest<ApiResult<IReadOnlyList<MoiJobDto>>>;

public sealed class GetMoiJobsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetMoiJobsQuery, ApiResult<IReadOnlyList<MoiJobDto>>>
{
    public async Task<ApiResult<IReadOnlyList<MoiJobDto>>> Handle(
        GetMoiJobsQuery request, CancellationToken cancellationToken)
    {
        var jobs = await context.MoiJobs.AsNoTracking().Include(x => x.Lines).Include(x => x.Events).Include(x => x.Payments)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return ApiResult<IReadOnlyList<MoiJobDto>>.Success(jobs.Select(MoiJobRules.ToDto).ToList());
    }
}

/// <summary>How many finished jobs are waiting to be billed — every MOI manager can send a bill, so everyone sees the same number. Drives the red number beside MOI Jobs in the sidebar.</summary>
public sealed record GetJobsAwaitingCloseQuery : IRequest<ApiResult<int>>;

public sealed class GetJobsAwaitingCloseQueryHandler(IApplicationDbContext context, ICurrentUser actor)
    : IRequestHandler<GetJobsAwaitingCloseQuery, ApiResult<int>>
{
    public async Task<ApiResult<int>> Handle(GetJobsAwaitingCloseQuery request, CancellationToken cancellationToken)
    {
        // Finished and waiting for the issuer to send, or sent and the client asked for changes.
        // Every MOI manager can send a bill, so they all see the same count.
        var waiting = context.MoiJobs.AsNoTracking().Where(x => x.Status == "Completed" || x.Status == "ChangesRequested");
        return ApiResult<int>.Success(await waiting.CountAsync(cancellationToken));
    }
}

/// <summary>The signed-in technician's own jobs — only those assigned to them.</summary>
public sealed record GetMyMoiJobsQuery : IRequest<ApiResult<IReadOnlyList<MoiJobDto>>>;

public sealed class GetMyMoiJobsQueryHandler(IApplicationDbContext context, ICurrentUser actor)
    : IRequestHandler<GetMyMoiJobsQuery, ApiResult<IReadOnlyList<MoiJobDto>>>
{
    public async Task<ApiResult<IReadOnlyList<MoiJobDto>>> Handle(
        GetMyMoiJobsQuery request, CancellationToken cancellationToken)
    {
        var jobs = await context.MoiJobs.AsNoTracking().Include(x => x.Lines).Include(x => x.Events).Include(x => x.Payments)
            .Where(x => x.AssignedToId == actor.UserId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return ApiResult<IReadOnlyList<MoiJobDto>>.Success(jobs.Select(MoiJobRules.ToDto).ToList());
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
        var staff = await MoiJobRules.Technicians(context)
            .OrderBy(s => s.FullName)
            .Select(s => new { Id = s.AdminUserId!.Value, s.FullName, s.Account!.Role })
            .ToListAsync(cancellationToken);
        var ids = staff.Select(s => s.Id).ToList();
        var grants = await context.UserPermissions.AsNoTracking().Where(p => ids.Contains(p.AdminUserId))
            .Select(p => new { p.AdminUserId, p.Permission }).ToListAsync(cancellationToken);
        // Only technicians who will actually see the job in their portal can be picked.
        var technicians = staff
            .Where(s => Jama.Application.Common.Permissions.EffectiveFor(s.Role, grants.Where(g => g.AdminUserId == s.Id).Select(g => g.Permission))
                .Contains(Jama.Application.Common.Permissions.MoiJobWork))
            .Select(s => new JobTechnicianDto(s.Id, s.FullName))
            .ToList();
        return ApiResult<IReadOnlyList<JobTechnicianDto>>.Success(technicians);
    }
}
