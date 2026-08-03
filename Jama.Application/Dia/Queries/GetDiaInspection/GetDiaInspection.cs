using AutoMapper;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Dia.Queries.GetDiaInspection;

public sealed record GetDiaInspectionQuery(Guid Id) : IRequest<ApiResult<DiaInspectionDto>>;

public sealed class GetDiaInspectionHandler(
    IDiaInspectionRepository repository,
    IApplicationDbContext context,
    IDiaInspectionCalculator calculator,
    IMapper mapper) : IRequestHandler<GetDiaInspectionQuery, ApiResult<DiaInspectionDto>>
{
    public async Task<ApiResult<DiaInspectionDto>> Handle(GetDiaInspectionQuery request, CancellationToken cancellationToken)
    {
        var entity = await repository.Inspections.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.Id && !x.IsArchived, cancellationToken);
        if (entity is null) return ApiResult<DiaInspectionDto>.Failure("DIA inspection not found.");

        var names = await context.AdminUsers.AsNoTracking()
            .Where(x => x.Id == entity.CreatedById || x.Id == entity.UpdatedById)
            .ToDictionaryAsync(x => x.Id, x => x.FullName + " <" + x.Email + ">", cancellationToken);
        names.TryGetValue(entity.CreatedById, out var createdBy);
        var updatedBy = entity.UpdatedById is { } id && names.TryGetValue(id, out var name) ? name : null;

        var submitted = (await repository.GetSubmittedQuarterCountsAsync([entity.Id], cancellationToken))
            .GetValueOrDefault(entity.Id);
        return ApiResult<DiaInspectionDto>.Success(
            DiaRequestSupport.ToDto(entity, calculator, mapper, submitted, createdBy, updatedBy));
    }
}
