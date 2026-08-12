using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TechnicianInspectionStatus = Jama.Domain.Enums.TechnicianInspectionStatus;

namespace Jama.Application.Technician.Queries.GetTechnicianDiaList;

public sealed record GetTechnicianDiaListQuery : IRequest<ApiResult<IReadOnlyList<TechnicianDiaListItemDto>>>;

public sealed class GetTechnicianDiaListHandler(
    ITechnicianInspectionRepository repository,
    ITechnicianInspectionCalculator calculator)
    : IRequestHandler<GetTechnicianDiaListQuery, ApiResult<IReadOnlyList<TechnicianDiaListItemDto>>>
{
    public async Task<ApiResult<IReadOnlyList<TechnicianDiaListItemDto>>> Handle(
        GetTechnicianDiaListQuery request,
        CancellationToken cancellationToken)
    {
        var dias = await repository.ActiveDiaInspections
            .OrderByDescending(x => x.ActivatedDate)
            .ThenBy(x => x.DiaNumber)
            .ToListAsync(cancellationToken);

        var diaIds = dias.Select(x => x.Id).ToList();
        var inspections = await repository.Inspections.AsNoTracking()
            .Where(x => diaIds.Contains(x.DiaInspectionId))
            .ToListAsync(cancellationToken);

        var submittedByDia = inspections
            .Where(x => x.Status == TechnicianInspectionStatus.Submitted)
            .GroupBy(x => x.DiaInspectionId)
            .ToDictionary(g => g.Key, g => g.Count());

        var items = dias.Select(dia =>
        {
            var submitted = Math.Clamp(submittedByDia.GetValueOrDefault(dia.Id), 0, 4);
            var cycle = calculator.Calculate(dia.InspectionStartedDate ?? dia.ActivatedDate, submitted);
            var quarterInspection = cycle.CurrentQuarter is { } q
                ? inspections.FirstOrDefault(x => x.DiaInspectionId == dia.Id && x.Quarter == q)
                : null;

            return new TechnicianDiaListItemDto(
                dia.Id,
                dia.DiaNumber,
                dia.ClientNumber,
                dia.ClientName,
                dia.ClientLocation,
                dia.ActivatedDate,
                cycle.Status,
                cycle.CurrentQuarter,
                TechnicianSupport.ResolveAction(cycle.Status, cycle.CurrentQuarter, quarterInspection),
                quarterInspection?.Id,
                submitted,
                cycle.QuarterStartDate,
                cycle.QuarterEndDate,
                cycle.RemainingDays,
                cycle.ProgressPercent,
                cycle.OverdueQuarters,
                TechnicianSupport.QuarterDates(dia.InspectionStartedDate ?? dia.ActivatedDate));
        }).ToList();

        return ApiResult<IReadOnlyList<TechnicianDiaListItemDto>>.Success(items);
    }
}
