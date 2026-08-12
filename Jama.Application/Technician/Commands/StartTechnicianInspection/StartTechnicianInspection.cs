using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TechnicianInspectionStatus = Jama.Domain.Enums.TechnicianInspectionStatus;

namespace Jama.Application.Technician.Commands.StartTechnicianInspection;

public sealed record StartTechnicianInspectionCommand(Guid DiaInspectionId)
    : IRequest<ApiResult<TechnicianInspectionDto>>;

public sealed class StartTechnicianInspectionHandler(
    ITechnicianInspectionRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUser actor,
    TimeProvider timeProvider,
    ITechnicianInspectionCalculator calculator)
    : IRequestHandler<StartTechnicianInspectionCommand, ApiResult<TechnicianInspectionDto>>
{
    public async Task<ApiResult<TechnicianInspectionDto>> Handle(
        StartTechnicianInspectionCommand request,
        CancellationToken cancellationToken)
    {
        var dia = await repository.FindActiveDiaAsync(request.DiaInspectionId, cancellationToken);
        if (dia is null)
            return ApiResult<TechnicianInspectionDto>.Failure("Activated DIA inspection not found.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (dia.InspectionStartedDate is null)
        {
            dia.InspectionStartedDate = now;
            dia.UpdatedAt = now;
        }

        var submittedQuarters = await repository.Inspections
            .CountAsync(x => x.DiaInspectionId == dia.Id && x.Status == TechnicianInspectionStatus.Submitted, cancellationToken);
        var cycle = calculator.Calculate(dia.InspectionStartedDate ?? dia.ActivatedDate, submittedQuarters);
        if (cycle.CurrentQuarter is not { } quarter)
            return ApiResult<TechnicianInspectionDto>.Failure("Inspection cycle is not active.");

        var existing = await repository.FindQuarterInspectionAsync(dia.Id, quarter, cancellationToken);
        if (existing?.Status == TechnicianInspectionStatus.Submitted)
            return ApiResult<TechnicianInspectionDto>.Failure("Current quarter inspection is already submitted.");

        if (existing is not null)
        {
            var loaded = await repository.FindInspectionAsync(existing.Id, cancellationToken);
            return ApiResult<TechnicianInspectionDto>.Success(
                TechnicianSupport.ToDto(loaded ?? existing));
        }

        var entity = new TechnicianInspection
        {
            Id = Guid.CreateVersion7(),
            DiaInspectionId = dia.Id,
            Quarter = quarter,
            TechnicianId = actor.UserId,
            Status = TechnicianInspectionStatus.Draft,
            CreatedAt = now,
        };

        repository.Add(entity);
        repository.AddHistory(TechnicianSupport.Audit(
            entity, TechnicianInspectionAction.Start, actor, null, TechnicianSupport.Snapshot(entity)));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResult<TechnicianInspectionDto>.Success(TechnicianSupport.ToDto(entity));
    }
}
