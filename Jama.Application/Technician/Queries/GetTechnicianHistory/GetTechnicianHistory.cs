using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Technician.Queries.GetTechnicianHistory;

public sealed record GetTechnicianHistoryQuery : IRequest<ApiResult<PaginatedResult<TechnicianInspectionHistoryDto>>>
{
    // Nullable because [AsParameters] treats non-nullable value types as
    // required query parameters — see GetDiaInspectionsQuery.
    public int? PageNumber { get; init; }
    public int? PageSize { get; init; }
    public Guid? DiaId { get; init; }
}

public sealed class GetTechnicianHistoryHandler(ITechnicianInspectionRepository repository)
    : IRequestHandler<GetTechnicianHistoryQuery, ApiResult<PaginatedResult<TechnicianInspectionHistoryDto>>>
{
    public async Task<ApiResult<PaginatedResult<TechnicianInspectionHistoryDto>>> Handle(
        GetTechnicianHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.PageNumber ?? 1);
        var size = Math.Clamp(request.PageSize ?? 20, 1, 100);
        var query = repository.History;
        if (request.DiaId is { } diaId)
            query = query.Where(x => x.DiaInspectionId == diaId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(x => new TechnicianInspectionHistoryDto(
                x.Id, x.TechnicianInspectionId, x.DiaInspectionId,
                x.Action.ToString(), x.ActorId, x.ActorName,
                x.CreatedAt, x.BeforeJson, x.AfterJson))
            .ToListAsync(cancellationToken);

        return ApiResult<PaginatedResult<TechnicianInspectionHistoryDto>>.Success(
            new(items, total, page, size, (int)Math.Ceiling(total / (double)size)));
    }
}
