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
    ITechnicianInspectionRepository repository)
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

        // Returns what has been recorded so far rather than only a finished cycle.
        // The admin detail screen shows this as "quarterly inspection details", and
        // withholding it until all four quarters were in meant a site two quarters
        // through reported "no quarters have been submitted yet" directly beneath a
        // header reading 50% — the same two submissions, described twice, disagreeing.
        //
        // A cycle still in progress simply has fewer inspections in the list; nothing
        // downstream requires four. Invoices and history are already whatever exists.

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
