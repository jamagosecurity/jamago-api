using System.Text.Json;
using AutoMapper;
using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Dia;

public sealed record CreateDiaInspectionCommand : IRequest<ApiResult<DiaInspectionDto>>
{
    public string? DiaNumber { get; init; }
    public string? ClientNumber { get; init; }
    public string? ClientName { get; init; }
    public string? ClientLocation { get; init; }
}

public sealed record UpdateDiaInspectionCommand : IRequest<ApiResult<DiaInspectionDto>>
{
    public Guid Id { get; init; }
    public string? DiaNumber { get; init; }
    public string? ClientNumber { get; init; }
    public string? ClientName { get; init; }
    public string? ClientLocation { get; init; }
}

public sealed record ChangeDiaInspectionStateCommand(Guid Id, DiaMutation Mutation)
    : IRequest<ApiResult<DiaInspectionDto>>;

public enum DiaMutation { Activate, Deactivate, Archive }

public sealed record GetDiaInspectionQuery(Guid Id) : IRequest<ApiResult<DiaInspectionDto>>;

public sealed record GetDiaInspectionsQuery : IRequest<ApiResult<PaginatedResult<DiaInspectionDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public DiaStatus? Status { get; init; }
    public string? SortBy { get; init; } = "createdDate";
    public string? SortDirection { get; init; } = "desc";
}

public sealed record GetDiaDashboardQuery : IRequest<ApiResult<DiaDashboardDto>>;

public sealed record GetDiaHistoryQuery : IRequest<ApiResult<PaginatedResult<DiaInspectionHistoryDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? DiaId { get; init; }
    public DiaInspectionAction? Action { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
}

public sealed class CreateDiaInspectionValidator : AbstractValidator<CreateDiaInspectionCommand>
{
    public CreateDiaInspectionValidator() => AddRules();
    private void AddRules()
    {
        RuleFor(x => x.DiaNumber).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x)).MaximumLength(100);
        RuleFor(x => x.ClientNumber).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x)).MaximumLength(100);
        RuleFor(x => x.ClientName).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x)).MaximumLength(200);
        RuleFor(x => x.ClientLocation).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x)).MaximumLength(300);
    }
}

public sealed class UpdateDiaInspectionValidator : AbstractValidator<UpdateDiaInspectionCommand>
{
    public UpdateDiaInspectionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DiaNumber).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x)).MaximumLength(100);
        RuleFor(x => x.ClientNumber).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x)).MaximumLength(100);
        RuleFor(x => x.ClientName).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x)).MaximumLength(200);
        RuleFor(x => x.ClientLocation).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x)).MaximumLength(300);
    }
}

public sealed class GetDiaInspectionsValidator : AbstractValidator<GetDiaInspectionsQuery>
{
    private static readonly string[] SortFields =
    [
        "createdDate", "updatedDate", "diaNumber", "clientNumber", "clientName",
        "clientLocation", "activatedDate", "currentQuarter", "nextInspectionDate", "status",
    ];

    public GetDiaInspectionsValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.SortBy).Must(x => string.IsNullOrWhiteSpace(x)
            || SortFields.Contains(x, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"sortBy must be one of: {string.Join(", ", SortFields)}.");
        RuleFor(x => x.SortDirection).Must(x => string.IsNullOrWhiteSpace(x)
            || x.Equals("asc", StringComparison.OrdinalIgnoreCase)
            || x.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("sortDirection must be asc or desc.");
    }
}

public sealed class GetDiaHistoryValidator : AbstractValidator<GetDiaHistoryQuery>
{
    public GetDiaHistoryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x).Must(x => !x.FromDate.HasValue || !x.ToDate.HasValue || x.FromDate <= x.ToDate)
            .WithMessage("fromDate must be before or equal to toDate.");
    }
}

internal static class DiaRequestSupport
{
    public static string Normalize(string value) => value.Trim().ToUpperInvariant();

    public static object Snapshot(DiaInspection x) => new
    {
        x.Id, x.DiaNumber, x.ClientNumber, x.ClientName, x.ClientLocation,
        x.ActivatedDate, x.IsActive, x.IsArchived, x.CreatedById, x.UpdatedById,
        x.CreatedAt, x.UpdatedAt,
    };

    public static string Json(object value) => JsonSerializer.Serialize(value);

    public static DiaInspectionDto ToDto(
        DiaInspection entity,
        IDiaInspectionCalculator calculator,
        IMapper mapper,
        string? createdBy = null,
        string? updatedBy = null)
    {
        var calculation = calculator.Calculate(entity.IsActive, entity.ActivatedDate);
        return mapper.Map<DiaInspectionDto>(entity) with
        {
            Status = calculation.Status,
            CurrentQuarter = calculation.CurrentQuarter,
            QuarterStartDate = calculation.QuarterStartDate,
            QuarterEndDate = calculation.QuarterEndDate,
            NextInspectionDate = calculation.NextInspectionDate,
            RemainingDays = calculation.RemainingDays,
            ProgressPercent = calculation.ProgressPercent,
            CreatedBy = createdBy ?? entity.CreatedById.ToString(),
            UpdatedBy = entity.UpdatedById is null ? null : updatedBy ?? entity.UpdatedById.Value.ToString(),
        };
    }

    public static DiaInspectionHistory Audit(
        DiaInspection entity,
        DiaInspectionAction action,
        ICurrentUser actor,
        object? before,
        object? after) => new()
    {
        Id = Guid.CreateVersion7(),
        DiaInspectionId = entity.Id,
        Action = action,
        ActorId = actor.UserId,
        ActorName = actor.DisplayName,
        BeforeJson = before is null ? null : Json(before),
        AfterJson = after is null ? null : Json(after),
        CreatedAt = DateTime.UtcNow,
    };
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
            return ApiResult<DiaInspectionDto>.Failure("A DIA inspection with this DIA number already exists.");
        }
        return ApiResult<DiaInspectionDto>.Success(
            DiaRequestSupport.ToDto(entity, calculator, mapper, actor.DisplayName));
    }
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
        return ApiResult<DiaInspectionDto>.Success(
            DiaRequestSupport.ToDto(entity, calculator, mapper, null, actor.DisplayName));
    }
}

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
        if (entity is null || entity.IsArchived)
            return ApiResult<DiaInspectionDto>.Failure("DIA inspection not found.");
        if (request.Mutation == DiaMutation.Activate && entity.IsActive)
            return ApiResult<DiaInspectionDto>.Failure("DIA inspection is already active.");

        var before = DiaRequestSupport.Snapshot(entity);
        var action = request.Mutation switch
        {
            DiaMutation.Activate => DiaInspectionAction.Activate,
            DiaMutation.Deactivate => DiaInspectionAction.Deactivate,
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
        return ApiResult<DiaInspectionDto>.Success(
            DiaRequestSupport.ToDto(entity, calculator, mapper, null, actor.DisplayName));
    }
}

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
        return ApiResult<DiaInspectionDto>.Success(
            DiaRequestSupport.ToDto(entity, calculator, mapper, createdBy, updatedBy));
    }
}

public sealed class GetDiaInspectionsHandler(
    IDiaInspectionRepository repository,
    IApplicationDbContext context,
    IDiaInspectionCalculator calculator,
    TimeProvider timeProvider,
    IMapper mapper) : IRequestHandler<GetDiaInspectionsQuery, ApiResult<PaginatedResult<DiaInspectionDto>>>
{
    public async Task<ApiResult<PaginatedResult<DiaInspectionDto>>> Handle(GetDiaInspectionsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.PageNumber);
        var size = Math.Clamp(request.PageSize, 1, 100);
        var query = repository.Inspections.AsNoTracking().Where(x => !x.IsArchived);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(x => x.DiaNumber.ToLower().Contains(search)
                || x.ClientNumber.ToLower().Contains(search)
                || x.ClientName.ToLower().Contains(search)
                || x.ClientLocation.ToLower().Contains(search));
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (request.Status is { } status)
            query = ApplyStatusFilter(query, status, now);

        var total = await query.CountAsync(cancellationToken);
        var selected = await ApplySort(query, request.SortBy, request.SortDirection, now)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);
        var userIds = selected.Select(x => x.CreatedById)
            .Concat(selected.Where(x => x.UpdatedById.HasValue).Select(x => x.UpdatedById!.Value))
            .Distinct().ToList();
        var names = await context.AdminUsers.AsNoTracking().Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName + " <" + x.Email + ">", cancellationToken);
        var dtos = selected.Select(entity =>
        {
            names.TryGetValue(entity.CreatedById, out var createdBy);
            var updatedBy = entity.UpdatedById is { } id && names.TryGetValue(id, out var name) ? name : null;
            return DiaRequestSupport.ToDto(entity, calculator, mapper, createdBy, updatedBy);
        }).ToList();
        return ApiResult<PaginatedResult<DiaInspectionDto>>.Success(
            new(dtos, total, page, size, (int)Math.Ceiling(total / (double)size)));
    }

    private static IQueryable<DiaInspection> ApplyStatusFilter(
        IQueryable<DiaInspection> query,
        DiaStatus status,
        DateTime now) =>
        status switch
        {
            DiaStatus.Inactive => query.Where(x => !x.IsActive || x.ActivatedDate == null || x.ActivatedDate > now),
            DiaStatus.Quarter1 => query.Where(x => x.IsActive && x.ActivatedDate != null
                && x.ActivatedDate <= now && now < x.ActivatedDate.Value.AddMonths(3)),
            DiaStatus.Quarter2 => query.Where(x => x.IsActive && x.ActivatedDate != null
                && x.ActivatedDate.Value.AddMonths(3) <= now && now < x.ActivatedDate.Value.AddMonths(6)),
            DiaStatus.Quarter3 => query.Where(x => x.IsActive && x.ActivatedDate != null
                && x.ActivatedDate.Value.AddMonths(6) <= now && now < x.ActivatedDate.Value.AddMonths(9)),
            DiaStatus.Quarter4 => query.Where(x => x.IsActive && x.ActivatedDate != null
                && x.ActivatedDate.Value.AddMonths(9) <= now && now < x.ActivatedDate.Value.AddMonths(12)),
            DiaStatus.Completed => query.Where(x => x.IsActive && x.ActivatedDate != null
                && x.ActivatedDate.Value.AddMonths(12) <= now),
            _ => query,
        };

    private static IQueryable<DiaInspection> ApplySort(
        IQueryable<DiaInspection> source,
        string? sortBy,
        string? direction,
        DateTime now)
    {
        var descending = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
        var field = (sortBy ?? "createdDate").ToLowerInvariant();

        IOrderedQueryable<DiaInspection> ordered = field switch
        {
            "dianumber" => descending ? source.OrderByDescending(x => x.DiaNumber) : source.OrderBy(x => x.DiaNumber),
            "clientnumber" => descending ? source.OrderByDescending(x => x.ClientNumber) : source.OrderBy(x => x.ClientNumber),
            "clientname" => descending ? source.OrderByDescending(x => x.ClientName) : source.OrderBy(x => x.ClientName),
            "clientlocation" => descending ? source.OrderByDescending(x => x.ClientLocation) : source.OrderBy(x => x.ClientLocation),
            "activateddate" => descending ? source.OrderByDescending(x => x.ActivatedDate) : source.OrderBy(x => x.ActivatedDate),
            "currentquarter" => descending
                ? source.OrderByDescending(x => x.IsActive && x.ActivatedDate != null
                    && x.ActivatedDate <= now && now < x.ActivatedDate.Value.AddMonths(12)
                    ? x.ActivatedDate.Value.AddMonths(9) <= now ? 4
                        : x.ActivatedDate.Value.AddMonths(6) <= now ? 3
                        : x.ActivatedDate.Value.AddMonths(3) <= now ? 2 : 1
                    : 0)
                : source.OrderBy(x => x.IsActive && x.ActivatedDate != null
                    && x.ActivatedDate <= now && now < x.ActivatedDate.Value.AddMonths(12)
                    ? x.ActivatedDate.Value.AddMonths(9) <= now ? 4
                        : x.ActivatedDate.Value.AddMonths(6) <= now ? 3
                        : x.ActivatedDate.Value.AddMonths(3) <= now ? 2 : 1
                    : 0),
            "nextinspectiondate" => descending
                ? source.OrderByDescending(x => x.IsActive && x.ActivatedDate != null
                    && x.ActivatedDate <= now && now < x.ActivatedDate.Value.AddMonths(12)
                    ? now < x.ActivatedDate.Value.AddMonths(3) ? x.ActivatedDate.Value.AddMonths(3)
                        : now < x.ActivatedDate.Value.AddMonths(6) ? x.ActivatedDate.Value.AddMonths(6)
                        : now < x.ActivatedDate.Value.AddMonths(9) ? x.ActivatedDate.Value.AddMonths(9)
                        : x.ActivatedDate.Value.AddMonths(12)
                    : (DateTime?)null)
                : source.OrderBy(x => x.IsActive && x.ActivatedDate != null
                    && x.ActivatedDate <= now && now < x.ActivatedDate.Value.AddMonths(12)
                    ? now < x.ActivatedDate.Value.AddMonths(3) ? x.ActivatedDate.Value.AddMonths(3)
                        : now < x.ActivatedDate.Value.AddMonths(6) ? x.ActivatedDate.Value.AddMonths(6)
                        : now < x.ActivatedDate.Value.AddMonths(9) ? x.ActivatedDate.Value.AddMonths(9)
                        : x.ActivatedDate.Value.AddMonths(12)
                    : (DateTime?)null),
            "updateddate" => descending ? source.OrderByDescending(x => x.UpdatedAt) : source.OrderBy(x => x.UpdatedAt),
            "status" => descending
                ? source.OrderByDescending(x => x.IsActive && x.ActivatedDate != null
                    ? x.ActivatedDate.Value.AddMonths(12) <= now ? 5
                        : x.ActivatedDate.Value.AddMonths(9) <= now ? 4
                        : x.ActivatedDate.Value.AddMonths(6) <= now ? 3
                        : x.ActivatedDate.Value.AddMonths(3) <= now ? 2 : 1
                    : 0)
                : source.OrderBy(x => x.IsActive && x.ActivatedDate != null
                    ? x.ActivatedDate.Value.AddMonths(12) <= now ? 5
                        : x.ActivatedDate.Value.AddMonths(9) <= now ? 4
                        : x.ActivatedDate.Value.AddMonths(6) <= now ? 3
                        : x.ActivatedDate.Value.AddMonths(3) <= now ? 2 : 1
                    : 0),
            _ => descending ? source.OrderByDescending(x => x.CreatedAt) : source.OrderBy(x => x.CreatedAt),
        };

        return ordered.ThenBy(x => x.Id);
    }
}

public sealed class GetDiaDashboardHandler(
    IDiaInspectionRepository repository,
    IDiaInspectionCalculator calculator) : IRequestHandler<GetDiaDashboardQuery, ApiResult<DiaDashboardDto>>
{
    public async Task<ApiResult<DiaDashboardDto>> Handle(GetDiaDashboardQuery request, CancellationToken cancellationToken)
    {
        var rows = await repository.Inspections.AsNoTracking().Where(x => !x.IsArchived)
            .Select(x => new { x.IsActive, x.ActivatedDate }).ToListAsync(cancellationToken);
        var statuses = rows.Select(x => calculator.Calculate(x.IsActive, x.ActivatedDate).Status).ToList();
        var active = statuses.Count(x =>
            x is DiaStatus.Quarter1 or DiaStatus.Quarter2 or DiaStatus.Quarter3 or DiaStatus.Quarter4);
        return ApiResult<DiaDashboardDto>.Success(new(
            rows.Count,
            active,
            statuses.Count(x => x == DiaStatus.Inactive),
            statuses.Count(x => x == DiaStatus.Quarter1),
            statuses.Count(x => x == DiaStatus.Quarter2),
            statuses.Count(x => x == DiaStatus.Quarter3),
            statuses.Count(x => x == DiaStatus.Quarter4),
            statuses.Count(x => x == DiaStatus.Completed)));
    }
}

public sealed class GetDiaHistoryHandler(IDiaInspectionRepository repository)
    : IRequestHandler<GetDiaHistoryQuery, ApiResult<PaginatedResult<DiaInspectionHistoryDto>>>
{
    public async Task<ApiResult<PaginatedResult<DiaInspectionHistoryDto>>> Handle(GetDiaHistoryQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.PageNumber);
        var size = Math.Clamp(request.PageSize, 1, 100);
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
