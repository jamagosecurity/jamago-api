using AutoMapper;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;

namespace Jama.Application.Dia.Commands.ChangeDiaInspectionState;

/// <summary>
/// The four lifecycle transitions a DIA record can go through. Kept as one
/// command rather than four because they share the same guards, audit trail and
/// projection — splitting them duplicated all three and let them drift.
/// </summary>
public enum DiaMutation { Activate, Deactivate, Archive, Restore }

public sealed record ChangeDiaInspectionStateCommand(Guid Id, DiaMutation Mutation)
    : IRequest<ApiResult<DiaInspectionDto>>;

public sealed class ChangeDiaInspectionStateHandler(
    IDiaInspectionRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUser actor,
    TimeProvider timeProvider,
    IDiaInspectionCalculator calculator,
    IMapper mapper) : IRequestHandler<ChangeDiaInspectionStateCommand, ApiResult<DiaInspectionDto>>
{
    public async Task<ApiResult<DiaInspectionDto>> Handle(ChangeDiaInspectionStateCommand request, CancellationToken cancellationToken)
    {
        var entity = await repository.FindAsync(request.Id, cancellationToken);
        if (entity is null)
            return ApiResult<DiaInspectionDto>.Failure("DIA inspection not found.");

        // Restore is the one mutation that operates ON an archived record;
        // every other one treats archived as gone.
        if (request.Mutation == DiaMutation.Restore)
        {
            if (!entity.IsArchived)
                return ApiResult<DiaInspectionDto>.Failure("DIA inspection is not archived.");
        }
        else if (entity.IsArchived)
        {
            return ApiResult<DiaInspectionDto>.Failure("DIA inspection not found.");
        }

        if (request.Mutation == DiaMutation.Activate && entity.IsActive)
            return ApiResult<DiaInspectionDto>.Failure("DIA inspection is already active.");

        var before = DiaRequestSupport.Snapshot(entity);
        var action = request.Mutation switch
        {
            DiaMutation.Activate => DiaInspectionAction.Activate,
            DiaMutation.Deactivate => DiaInspectionAction.Deactivate,
            DiaMutation.Restore => DiaInspectionAction.Restore,
            _ => DiaInspectionAction.Archive,
        };

        if (request.Mutation == DiaMutation.Activate)
        {
            entity.IsActive = true;
            entity.ActivatedDate ??= timeProvider.GetUtcNow().UtcDateTime;
        }
        else if (request.Mutation == DiaMutation.Deactivate)
        {
            entity.IsActive = false;
        }
        else if (request.Mutation == DiaMutation.Restore)
        {
            // Comes back inactive: the admin re-activates deliberately, so a
            // restore never silently restarts a technician's quarterly clock.
            entity.IsArchived = false;
            entity.IsActive = false;
        }
        else
        {
            entity.IsArchived = true;
            entity.IsActive = false;
        }

        entity.UpdatedById = actor.UserId;
        entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        repository.AddHistory(DiaRequestSupport.Audit(
            entity, action, actor, before, DiaRequestSupport.Snapshot(entity)));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var submitted = (await repository.GetSubmittedQuarterCountsAsync([entity.Id], cancellationToken))
            .GetValueOrDefault(entity.Id);
        return ApiResult<DiaInspectionDto>.Success(
            DiaRequestSupport.ToDto(entity, calculator, mapper, submitted, null, actor.DisplayName));
    }
}
