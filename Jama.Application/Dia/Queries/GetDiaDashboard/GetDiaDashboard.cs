using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Dia.Queries.GetDiaDashboard;

public sealed record GetDiaDashboardQuery : IRequest<ApiResult<DiaDashboardDto>>;

public sealed class GetDiaDashboardHandler(
    IDiaInspectionRepository repository,
    IDiaInspectionCalculator calculator) : IRequestHandler<GetDiaDashboardQuery, ApiResult<DiaDashboardDto>>
{
    public async Task<ApiResult<DiaDashboardDto>> Handle(GetDiaDashboardQuery request, CancellationToken cancellationToken)
    {
        var rows = await repository.Inspections.AsNoTracking().Where(x => !x.IsArchived)
            .Select(x => new { x.Id, x.IsActive, x.ActivatedDate }).ToListAsync(cancellationToken);
        var counts = await repository.GetSubmittedQuarterCountsAsync(
            rows.Select(x => x.Id).ToList(), cancellationToken);
        var statuses = rows
            .Select(x => calculator.Calculate(x.IsActive, x.ActivatedDate, counts.GetValueOrDefault(x.Id)).Status)
            .ToList();
        var active = statuses.Count(x =>
            x is DiaStatus.Quarter1 or DiaStatus.Quarter2 or DiaStatus.Quarter3 or DiaStatus.Quarter4);
        return ApiResult<DiaDashboardDto>.Success(new(
            rows.Count,
            active,
            statuses.Count(x => x == DiaStatus.Inactive),
            statuses.Count(x => x == DiaStatus.Quarter1),
            statuses.Count(x => x == DiaStatus.Quarter2),
            statuses.Count(x => x == DiaStatus.Quarter3),
            statuses.Count(x => x == DiaStatus.Quarter4),
            statuses.Count(x => x == DiaStatus.Completed)));
    }
}
