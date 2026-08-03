using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using TechnicianInspectionStatus = Jama.Domain.Enums.TechnicianInspectionStatus;

namespace Jama.Application.Technician.Commands.ReopenTechnicianInspection;

public sealed record ReopenTechnicianInspectionCommand(Guid InspectionId)
    : IRequest<ApiResult<TechnicianInspectionDto>>;

public sealed class ReopenTechnicianInspectionHandler(
    ITechnicianInspectionRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUser actor,
    TimeProvider timeProvider)
    : IRequestHandler<ReopenTechnicianInspectionCommand, ApiResult<TechnicianInspectionDto>>
{
    public async Task<ApiResult<TechnicianInspectionDto>> Handle(
        ReopenTechnicianInspectionCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await repository.FindInspectionAsync(request.InspectionId, cancellationToken);
        if (entity is null)
            return ApiResult<TechnicianInspectionDto>.Failure("Inspection not found.");

        if (entity.Status != TechnicianInspectionStatus.Submitted)
            return ApiResult<TechnicianInspectionDto>.Failure("Only submitted inspections can be reopened.");

        var before = TechnicianSupport.Snapshot(entity);
        entity.Status = TechnicianInspectionStatus.Draft;
        entity.SubmittedAt = null;
        entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        repository.AddHistory(TechnicianSupport.Audit(
            entity, TechnicianInspectionAction.Reopen, actor, before, TechnicianSupport.Snapshot(entity)));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResult<TechnicianInspectionDto>.Success(TechnicianSupport.ToDto(entity));
    }
}
