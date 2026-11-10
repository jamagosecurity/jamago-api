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
        var jobs = await context.NonMoiJobs.AsNoTracking().Include(x => x.Lines)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return ApiResult<IReadOnlyList<NonMoiJobDto>>.Success(jobs.Select(NonMoiJobRules.ToDto).ToList());
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
        var jobs = await context.NonMoiJobs.AsNoTracking().Include(x => x.Lines)
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
