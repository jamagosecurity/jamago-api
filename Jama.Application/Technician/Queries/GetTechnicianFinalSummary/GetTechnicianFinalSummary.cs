using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TechnicianInspectionStatus = Jama.Domain.Enums.TechnicianInspectionStatus;

namespace Jama.Application.Technician.Queries.GetTechnicianFinalSummary;

public sealed record GetTechnicianFinalSummaryQuery(Guid DiaInspectionId)
    : IRequest<ApiResult<TechnicianFinalSummaryDto>>;

public sealed class GetTechnicianFinalSummaryHandler(
    ITechnicianInspectionRepository repository,
    ITechnicianInspectionCalculator calculator)
    : IRequestHandler<GetTechnicianFinalSummaryQuery, ApiResult<TechnicianFinalSummaryDto>>
{
    public async Task<ApiResult<TechnicianFinalSummaryDto>> Handle(
        GetTechnicianFinalSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var dia = await repository.FindActiveDiaAsync(request.DiaInspectionId, cancellationToken);
        if (dia is null)
            return ApiResult<TechnicianFinalSummaryDto>.Failure("Activated DIA inspection not found.");

        var inspections = await repository.Inspections.AsNoTracking()
            .Include(x => x.Cameras)
            .Include(x => x.Network)
            .Include(x => x.Vms)
            .Include(x => x.UpsGeneral)
            .Include(x => x.Anpr)
            .Include(x => x.Kpoi)
            .Where(x => x.DiaInspectionId == dia.Id)
            .OrderBy(x => x.Quarter)
            .ToListAsync(cancellationToken);

        var submittedQuarters = inspections.Count(x => x.Status == TechnicianInspectionStatus.Submitted);
        var cycle = calculator.Calculate(dia.InspectionStartedDate, submittedQuarters);
        if (cycle.Status != TechnicianInspectionCycleStatus.Completed)
            return ApiResult<TechnicianFinalSummaryDto>.Failure("Final summary is available after the inspection cycle completes.");

        var invoices = await repository.Invoices
            .Where(x => x.DiaInspectionId == dia.Id)
            .OrderBy(x => x.Quarter)
            .Select(x => new InspectionInvoiceDto(
                x.Id, x.TechnicianInspectionId, x.DiaInspectionId,
                dia.DiaNumber, x.Quarter, x.InvoiceNumber, x.GeneratedAt))
            .ToListAsync(cancellationToken);

        var history = await repository.History
            .Where(x => x.DiaInspectionId == dia.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new TechnicianInspectionHistoryDto(
                x.Id, x.TechnicianInspectionId, x.DiaInspectionId,
                x.Action.ToString(), x.ActorId, x.ActorName,
                x.CreatedAt, x.BeforeJson, x.AfterJson))
            .ToListAsync(cancellationToken);

        return ApiResult<TechnicianFinalSummaryDto>.Success(new(
            dia.Id,
            dia.DiaNumber,
            dia.ClientName,
            dia.InspectionStartedDate,
            inspections.Select(TechnicianSupport.ToDto).ToList(),
            invoices,
            history));
    }
}
