using AutoMapper;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Dia.Commands.UpdateDiaInspection;

public sealed record UpdateDiaInspectionCommand : IRequest<ApiResult<DiaInspectionDto>>
{
    public Guid Id { get; init; }
    public string? DiaNumber { get; init; }
    public string? ClientNumber { get; init; }
    public string? ClientName { get; init; }
    public string? ClientLocation { get; init; }
}

public sealed class UpdateDiaInspectionHandler(
    IDiaInspectionRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUser actor,
    TimeProvider timeProvider,
    IDiaInspectionCalculator calculator,
    IMapper mapper) : IRequestHandler<UpdateDiaInspectionCommand, ApiResult<DiaInspectionDto>>
{
    public async Task<ApiResult<DiaInspectionDto>> Handle(UpdateDiaInspectionCommand request, CancellationToken cancellationToken)
    {
        var entity = await repository.FindAsync(request.Id, cancellationToken);
        if (entity is null || entity.IsArchived)
            return ApiResult<DiaInspectionDto>.Failure("DIA inspection not found.");

        var normalized = DiaRequestSupport.Normalize(request.DiaNumber!);
        if (await repository.DiaNumberExistsAsync(normalized, request.Id, cancellationToken))
            return ApiResult<DiaInspectionDto>.Failure("A DIA inspection with this DIA number already exists.");

        var before = DiaRequestSupport.Snapshot(entity);
        entity.DiaNumber = request.DiaNumber!.Trim();
        entity.NormalizedDiaNumber = normalized;
        entity.ClientNumber = request.ClientNumber!.Trim();
        entity.ClientName = request.ClientName!.Trim();
        entity.ClientLocation = request.ClientLocation!.Trim();
        entity.UpdatedById = actor.UserId;
        entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        repository.AddHistory(DiaRequestSupport.Audit(
            entity, DiaInspectionAction.Update, actor, before, DiaRequestSupport.Snapshot(entity)));
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ApiResult<DiaInspectionDto>.Failure("A DIA inspection with this DIA number already exists.");
        }

        var submitted = (await repository.GetSubmittedQuarterCountsAsync([entity.Id], cancellationToken))
            .GetValueOrDefault(entity.Id);
        return ApiResult<DiaInspectionDto>.Success(
            DiaRequestSupport.ToDto(entity, calculator, mapper, submitted, null, actor.DisplayName));
    }
}
