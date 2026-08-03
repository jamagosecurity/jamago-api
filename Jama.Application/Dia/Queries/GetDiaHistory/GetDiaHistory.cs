using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Dia.Queries.GetDiaHistory;

/// <summary>Nullable for the [AsParameters] reason on GetDiaInspectionsQuery.</summary>
public sealed record GetDiaHistoryQuery : IRequest<ApiResult<PaginatedResult<DiaInspectionHistoryDto>>>
{
    public int? PageNumber { get; init; }
    public int? PageSize { get; init; }
    public Guid? DiaId { get; init; }
    public DiaInspectionAction? Action { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
}

public sealed class GetDiaHistoryHandler(IDiaInspectionRepository repository)
    : IRequestHandler<GetDiaHistoryQuery, ApiResult<PaginatedResult<DiaInspectionHistoryDto>>>
{
    public async Task<ApiResult<PaginatedResult<DiaInspectionHistoryDto>>> Handle(GetDiaHistoryQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.PageNumber ?? 1);
        var size = Math.Clamp(request.PageSize ?? 20, 1, 100);
        var query = repository.History.AsNoTracking();
        if (request.DiaId is { } diaId) query = query.Where(x => x.DiaInspectionId == diaId);
        if (request.Action is { } action) query = query.Where(x => x.Action == action);
        if (request.FromDate is { } from) query = query.Where(x => x.CreatedAt >= from.ToUniversalTime());
        if (request.ToDate is { } to) query = query.Where(x => x.CreatedAt <= to.ToUniversalTime());
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * size).Take(size)
            .Select(x => new DiaInspectionHistoryDto(
                x.Id, x.DiaInspectionId, x.Action, x.ActorId, x.ActorName,
                x.CreatedAt, x.BeforeJson, x.AfterJson))
            .ToListAsync(cancellationToken);
        return ApiResult<PaginatedResult<DiaInspectionHistoryDto>>.Success(
            new(items, total, page, size, (int)Math.Ceiling(total / (double)size)));
    }
}
