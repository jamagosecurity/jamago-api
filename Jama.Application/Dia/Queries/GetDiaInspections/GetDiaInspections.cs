using AutoMapper;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Dia.Queries.GetDiaInspections;

/// <summary>
/// Bound with [AsParameters], which treats a non-nullable value type as a
/// REQUIRED query parameter and returns 500 when it is absent — the property
/// initialiser is never consulted. Every optional parameter here must therefore
/// be nullable, with its default applied in the handler.
/// </summary>
public sealed record GetDiaInspectionsQuery : IRequest<ApiResult<PaginatedResult<DiaInspectionDto>>>
{
    public int? PageNumber { get; init; }
    public int? PageSize { get; init; }
    public string? Search { get; init; }
    public DiaStatus? Status { get; init; }

    /// <summary>
    /// Archived records are excluded by default. Set true to list only archived
    /// ones — archiving is a soft delete, so this is the route back to a record
    /// that was archived by mistake.
    /// </summary>
    public bool? Archived { get; init; }
    public string? SortBy { get; init; }
    public string? SortDirection { get; init; }
}

public sealed class GetDiaInspectionsHandler(
    IDiaInspectionRepository repository,
    IApplicationDbContext context,
    IDiaInspectionCalculator calculator,
    IMapper mapper) : IRequestHandler<GetDiaInspectionsQuery, ApiResult<PaginatedResult<DiaInspectionDto>>>
{
    public async Task<ApiResult<PaginatedResult<DiaInspectionDto>>> Handle(GetDiaInspectionsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.PageNumber ?? 1);
        var size = Math.Clamp(request.PageSize ?? 20, 1, 100);
        var archived = request.Archived ?? false;
        var query = repository.Inspections.AsNoTracking().Where(x => x.IsArchived == archived);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(x => x.DiaNumber.ToLower().Contains(search)
                || x.ClientNumber.ToLower().Contains(search)
                || x.ClientName.ToLower().Contains(search)
                || x.ClientLocation.ToLower().Contains(search));
        }

        // DIA records are inherently low-volume (one per client site), so the submission-based
        // status is computed in memory rather than translated into the SQL filter/sort. This keeps
        // the list, dashboard and technician portal all reporting the same quarter.
        var all = await query.ToListAsync(cancellationToken);
        var counts = await repository.GetSubmittedQuarterCountsAsync(
            all.Select(x => x.Id).ToList(), cancellationToken);

        var withStatus = all
            .Select(x => (Dia: x, Calc: calculator.Calculate(x.IsActive, x.ActivatedDate, counts.GetValueOrDefault(x.Id))))
            .ToList();

        if (request.Status is { } status)
            withStatus = withStatus.Where(t => t.Calc.Status == status).ToList();

        var total = withStatus.Count;
        var pageItems = ApplySort(withStatus, request.SortBy, request.SortDirection)
            .Skip((page - 1) * size)
            .Take(size)
            .ToList();

        var userIds = pageItems.Select(t => t.Dia.CreatedById)
            .Concat(pageItems.Where(t => t.Dia.UpdatedById.HasValue).Select(t => t.Dia.UpdatedById!.Value))
            .Distinct().ToList();
        var names = await context.AdminUsers.AsNoTracking().Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName + " <" + x.Email + ">", cancellationToken);
        var dtos = pageItems.Select(t =>
        {
            names.TryGetValue(t.Dia.CreatedById, out var createdBy);
            var updatedBy = t.Dia.UpdatedById is { } id && names.TryGetValue(id, out var name) ? name : null;
            return DiaRequestSupport.ToDto(
                t.Dia, calculator, mapper, counts.GetValueOrDefault(t.Dia.Id), createdBy, updatedBy);
        }).ToList();
        return ApiResult<PaginatedResult<DiaInspectionDto>>.Success(
            new(dtos, total, page, size, (int)Math.Ceiling(total / (double)size)));
    }

    private static IEnumerable<(DiaInspection Dia, DiaCalculation Calc)> ApplySort(
        IReadOnlyList<(DiaInspection Dia, DiaCalculation Calc)> source,
        string? sortBy,
        string? direction)
    {
        var descending = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
        var field = (sortBy ?? "createdDate").ToLowerInvariant();

        Func<(DiaInspection Dia, DiaCalculation Calc), IComparable?> key = field switch
        {
            "dianumber" => t => t.Dia.DiaNumber,
            "clientnumber" => t => t.Dia.ClientNumber,
            "clientname" => t => t.Dia.ClientName,
            "clientlocation" => t => t.Dia.ClientLocation,
            "activateddate" => t => (IComparable?)t.Dia.ActivatedDate,
            "currentquarter" => t => t.Calc.CurrentQuarter ?? 0,
            "nextinspectiondate" => t => (IComparable?)t.Calc.NextInspectionDate,
            "updateddate" => t => (IComparable?)t.Dia.UpdatedAt,
            "status" => t => (int)t.Calc.Status,
            _ => t => (IComparable?)t.Dia.CreatedAt,
        };

        return descending
            ? source.OrderByDescending(key).ThenBy(t => t.Dia.Id)
            : source.OrderBy(key).ThenBy(t => t.Dia.Id);
    }
}
