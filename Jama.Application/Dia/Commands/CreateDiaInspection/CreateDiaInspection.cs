using AutoMapper;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Dia.Commands.CreateDiaInspection;

public sealed record CreateDiaInspectionCommand : IRequest<ApiResult<DiaInspectionDto>>
{
    public string? DiaNumber { get; init; }
    public string? ClientNumber { get; init; }
    public string? ClientName { get; init; }
    public string? ClientLocation { get; init; }
}

public sealed class CreateDiaInspectionHandler(
    IDiaInspectionRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUser actor,
    TimeProvider timeProvider,
    IDiaInspectionCalculator calculator,
    IMapper mapper) : IRequestHandler<CreateDiaInspectionCommand, ApiResult<DiaInspectionDto>>
{
    public async Task<ApiResult<DiaInspectionDto>> Handle(CreateDiaInspectionCommand request, CancellationToken cancellationToken)
    {
        var diaNumber = request.DiaNumber!.Trim();
        var normalized = DiaRequestSupport.Normalize(diaNumber);
        if (await repository.DiaNumberExistsAsync(normalized, null, cancellationToken))
            return ApiResult<DiaInspectionDto>.Failure("A DIA inspection with this DIA number already exists.");

        var entity = new DiaInspection
        {
            Id = Guid.CreateVersion7(),
            DiaNumber = diaNumber,
            NormalizedDiaNumber = normalized,
            ClientNumber = request.ClientNumber!.Trim(),
            ClientName = request.ClientName!.Trim(),
            ClientLocation = request.ClientLocation!.Trim(),
            CreatedById = actor.UserId,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        repository.Add(entity);
        repository.AddHistory(DiaRequestSupport.Audit(
            entity, DiaInspectionAction.Create, actor, null, DiaRequestSupport.Snapshot(entity)));
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The pre-check above races with concurrent creates; the unique index
            // is the real guard, so a violation here means the same thing.
            return ApiResult<DiaInspectionDto>.Failure("A DIA inspection with this DIA number already exists.");
        }

        // A freshly created DIA has no submitted quarters yet.
        return ApiResult<DiaInspectionDto>.Success(
            DiaRequestSupport.ToDto(entity, calculator, mapper, 0, actor.DisplayName));
    }
}
