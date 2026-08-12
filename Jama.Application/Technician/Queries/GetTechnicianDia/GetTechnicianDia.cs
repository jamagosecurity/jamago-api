using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TechnicianInspectionStatus = Jama.Domain.Enums.TechnicianInspectionStatus;

namespace Jama.Application.Technician.Queries.GetTechnicianDia;

public sealed record GetTechnicianDiaQuery(Guid Id) : IRequest<ApiResult<TechnicianDiaDetailDto>>;

public sealed class GetTechnicianDiaHandler(
    ITechnicianInspectionRepository repository,
    ITechnicianInspectionCalculator calculator)
    : IRequestHandler<GetTechnicianDiaQuery, ApiResult<TechnicianDiaDetailDto>>
{
    public async Task<ApiResult<TechnicianDiaDetailDto>> Handle(
        GetTechnicianDiaQuery request,
        CancellationToken cancellationToken)
    {
        var dia = await repository.FindActiveDiaAsync(request.Id, cancellationToken);
        if (dia is null)
            return ApiResult<TechnicianDiaDetailDto>.Failure("Activated DIA inspection not found.");

        var submittedQuarters = await repository.Inspections
            .CountAsync(x => x.DiaInspectionId == dia.Id && x.Status == TechnicianInspectionStatus.Submitted, cancellationToken);
        var cycle = calculator.Calculate(dia.InspectionStartedDate ?? dia.ActivatedDate, submittedQuarters);
        TechnicianInspection? quarterInspection = null;
        if (cycle.CurrentQuarter is { } quarter)
        {
            quarterInspection = await repository.FindQuarterInspectionAsync(
                dia.Id, quarter, cancellationToken);
        }

        var action = TechnicianSupport.ResolveAction(cycle.Status, cycle.CurrentQuarter, quarterInspection);

        return ApiResult<TechnicianDiaDetailDto>.Success(new(
            dia.Id,
            dia.DiaNumber,
            dia.ClientNumber,
            dia.ClientName,
            dia.ClientLocation,
            dia.Latitude,
            dia.Longitude,
            dia.ActivatedDate,
            dia.InspectionStartedDate,
            cycle.Status,
            cycle.CurrentQuarter,
            cycle.QuarterStartDate,
            cycle.QuarterEndDate,
            cycle.RemainingDays,
            cycle.ProgressPercent,
            action,
            quarterInspection?.Id,
            quarterInspection is null ? null : TechnicianSupport.ToDto(quarterInspection)));
    }
}
