using System.Text.Json;
using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TechnicianInspectionStatus = Jama.Domain.Entities.TechnicianInspectionStatus;

namespace Jama.Application.Technician;

public sealed record GetTechnicianDiaListQuery : IRequest<ApiResult<IReadOnlyList<TechnicianDiaListItemDto>>>;

public sealed record GetTechnicianInspectionQuery(Guid Id) : IRequest<ApiResult<TechnicianInspectionDto>>;

public sealed record GetTechnicianDiaQuery(Guid Id) : IRequest<ApiResult<TechnicianDiaDetailDto>>;

public sealed record StartTechnicianInspectionCommand(Guid DiaInspectionId)
    : IRequest<ApiResult<TechnicianInspectionDto>>;

public sealed record SaveTechnicianInspectionDraftCommand : IRequest<ApiResult<TechnicianInspectionDto>>
{
    public Guid InspectionId { get; init; }
    public IReadOnlyList<CameraDetailDto> Cameras { get; init; } = [];
    public NetworkDetailDto? Network { get; init; }
    public VmsDetailDto? Vms { get; init; }
    public UpsGeneralDetailDto? UpsGeneral { get; init; }
    public AnprConfigurationDto? Anpr { get; init; }
    public KpoiDetailDto? Kpoi { get; init; }
}

public sealed record SubmitTechnicianInspectionCommand(Guid InspectionId)
    : IRequest<ApiResult<TechnicianInspectionDto>>;

public sealed record GetTechnicianHistoryQuery : IRequest<ApiResult<PaginatedResult<TechnicianInspectionHistoryDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? DiaId { get; init; }
}

public sealed record GetTechnicianInvoicesQuery : IRequest<ApiResult<IReadOnlyList<InspectionInvoiceDto>>>
{
    public Guid? DiaId { get; init; }
}

public sealed record GetTechnicianFinalSummaryQuery(Guid DiaInspectionId)
    : IRequest<ApiResult<TechnicianFinalSummaryDto>>;

public sealed record ReopenTechnicianInspectionCommand(Guid InspectionId)
    : IRequest<ApiResult<TechnicianInspectionDto>>;

public sealed class SaveTechnicianInspectionDraftValidator : AbstractValidator<SaveTechnicianInspectionDraftCommand>
{
    public SaveTechnicianInspectionDraftValidator()
    {
        RuleFor(x => x.InspectionId).NotEmpty();
        RuleForEach(x => x.Cameras).ChildRules(camera =>
        {
            camera.RuleFor(x => x.Brand).NotEmpty().MaximumLength(120);
            camera.RuleFor(x => x.Model).NotEmpty().MaximumLength(120);
            camera.RuleFor(x => x.Quantity).GreaterThan(0);
        });
    }
}

internal static class TechnicianSupport
{
    public static string Json(object value) => JsonSerializer.Serialize(value);

    public static object Snapshot(TechnicianInspection x) => new
    {
        x.Id, x.DiaInspectionId, x.Quarter, x.TechnicianId, x.Status, x.SubmittedAt,
    };

    public static TechnicianInspectionHistory Audit(
        TechnicianInspection entity,
        TechnicianInspectionAction action,
        ICurrentUser actor,
        object? before,
        object? after) => new()
    {
        Id = Guid.CreateVersion7(),
        TechnicianInspectionId = entity.Id,
        DiaInspectionId = entity.DiaInspectionId,
        Action = action,
        ActorId = actor.UserId,
        ActorName = actor.DisplayName,
        BeforeJson = before is null ? null : Json(before),
        AfterJson = after is null ? null : Json(after),
        CreatedAt = DateTime.UtcNow,
    };

    public static TechnicianDiaAction ResolveAction(
        int? currentQuarter,
        TechnicianInspection? quarterInspection)
    {
        if (currentQuarter is null or <= 0)
            return TechnicianDiaAction.StartInspection;

        return quarterInspection?.Status switch
        {
            TechnicianInspectionStatus.Draft => TechnicianDiaAction.Continue,
            TechnicianInspectionStatus.Submitted => TechnicianDiaAction.View,
            _ => TechnicianDiaAction.StartInspection,
        };
    }

    public static TechnicianInspectionDto ToDto(TechnicianInspection entity) => new(
        entity.Id,
        entity.DiaInspectionId,
        entity.Quarter,
        (TechnicianInspectionStatus)entity.Status,
        entity.SubmittedAt,
        entity.Status == TechnicianInspectionStatus.Submitted,
        entity.Cameras.OrderBy(x => x.CreatedAt).Select(x => new CameraDetailDto(
            x.Id, x.Brand, x.Model, x.Quantity, x.Location, x.Remarks)).ToList(),
        entity.Network is null ? null : new NetworkDetailDto(
            entity.Network.SwitchBrand, entity.Network.SwitchModel,
            entity.Network.RouterBrand, entity.Network.RouterModel,
            entity.Network.Firewall, entity.Network.RackDetails, entity.Network.NetworkRemarks),
        entity.Vms is null ? null : new VmsDetailDto(
            entity.Vms.VmsName, entity.Vms.Version, entity.Vms.LicenseDetails,
            entity.Vms.ServerDetails, entity.Vms.HealthStatus, entity.Vms.Remarks),
        entity.UpsGeneral is null ? null : new UpsGeneralDetailDto(
            entity.UpsGeneral.UpsBrand, entity.UpsGeneral.UpsCapacity,
            entity.UpsGeneral.BatteryStatus, entity.UpsGeneral.GeneratorAvailable,
            entity.UpsGeneral.GeneratorDetails, entity.UpsGeneral.GeneralRemarks),
        entity.Anpr is null ? null : new AnprConfigurationDto(
            entity.Anpr.AnprInstalled, entity.Anpr.CameraDetails,
            entity.Anpr.Configuration, entity.Anpr.SoftwareVersion, entity.Anpr.Remarks),
        entity.Kpoi is null ? null : new KpoiDetailDto(
            entity.Kpoi.IvdIvss, entity.Kpoi.KpoiCamera,
            entity.Kpoi.Lens, entity.Kpoi.HardDisc));

    public static void ApplyDraft(
        TechnicianInspection entity,
        SaveTechnicianInspectionDraftCommand request,
        TimeProvider timeProvider)
    {
        ApplyCameras(entity, request.Cameras, timeProvider);

        entity.Network ??= new NetworkDetail { Id = Guid.CreateVersion7(), TechnicianInspectionId = entity.Id };
        ApplyNetwork(entity.Network, request.Network);

        entity.Vms ??= new VmsDetail { Id = Guid.CreateVersion7(), TechnicianInspectionId = entity.Id };
        ApplyVms(entity.Vms, request.Vms);

        entity.UpsGeneral ??= new UpsGeneralDetail { Id = Guid.CreateVersion7(), TechnicianInspectionId = entity.Id };
        ApplyUps(entity.UpsGeneral, request.UpsGeneral);

        entity.Anpr ??= new AnprConfiguration { Id = Guid.CreateVersion7(), TechnicianInspectionId = entity.Id };
        ApplyAnpr(entity.Anpr, request.Anpr);

        entity.Kpoi ??= new KpoiDetail { Id = Guid.CreateVersion7(), TechnicianInspectionId = entity.Id };
        ApplyKpoi(entity.Kpoi, request.Kpoi);

        entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
    }

    private static void ApplyCameras(
        TechnicianInspection entity,
        IReadOnlyList<CameraDetailDto> cameras,
        TimeProvider timeProvider)
    {
        // The draft save loads the inspection without its existing child rows and replaces them
        // wholesale (the old rows are removed set-based inside the transaction), so here we just
        // build the fresh camera rows from the request.
        entity.Cameras.Clear();
        foreach (var camera in cameras)
        {
            entity.Cameras.Add(new CameraDetail
            {
                Id = Guid.CreateVersion7(),
                TechnicianInspectionId = entity.Id,
                Brand = camera.Brand.Trim(),
                Model = camera.Model.Trim(),
                Quantity = camera.Quantity,
                Location = camera.Location?.Trim(),
                Remarks = camera.Remarks?.Trim(),
                CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            });
        }
    }

    private static void ApplyNetwork(NetworkDetail target, NetworkDetailDto? source)
    {
        target.SwitchBrand = source?.SwitchBrand?.Trim();
        target.SwitchModel = source?.SwitchModel?.Trim();
        target.RouterBrand = source?.RouterBrand?.Trim();
        target.RouterModel = source?.RouterModel?.Trim();
        target.Firewall = source?.Firewall?.Trim();
        target.RackDetails = source?.RackDetails?.Trim();
        target.NetworkRemarks = source?.NetworkRemarks?.Trim();
    }

    private static void ApplyVms(VmsDetail target, VmsDetailDto? source)
    {
        target.VmsName = source?.VmsName?.Trim();
        target.Version = source?.Version?.Trim();
        target.LicenseDetails = source?.LicenseDetails?.Trim();
        target.ServerDetails = source?.ServerDetails?.Trim();
        target.HealthStatus = source?.HealthStatus?.Trim();
        target.Remarks = source?.Remarks?.Trim();
    }

    private static void ApplyUps(UpsGeneralDetail target, UpsGeneralDetailDto? source)
    {
        target.UpsBrand = source?.UpsBrand?.Trim();
        target.UpsCapacity = source?.UpsCapacity?.Trim();
        target.BatteryStatus = source?.BatteryStatus?.Trim();
        target.GeneratorAvailable = source?.GeneratorAvailable ?? false;
        target.GeneratorDetails = source?.GeneratorDetails?.Trim();
        target.GeneralRemarks = source?.GeneralRemarks?.Trim();
    }

    private static void ApplyAnpr(AnprConfiguration target, AnprConfigurationDto? source)
    {
        target.AnprInstalled = source?.AnprInstalled ?? false;
        target.CameraDetails = source?.CameraDetails?.Trim();
        target.Configuration = source?.Configuration?.Trim();
        target.SoftwareVersion = source?.SoftwareVersion?.Trim();
        target.Remarks = source?.Remarks?.Trim();
    }

    private static void ApplyKpoi(KpoiDetail target, KpoiDetailDto? source)
    {
        target.IvdIvss = source?.IvdIvss?.Trim();
        target.KpoiCamera = source?.KpoiCamera?.Trim();
        target.Lens = source?.Lens?.Trim();
        target.HardDisc = source?.HardDisc?.Trim();
    }

    public static string GenerateInvoiceNumber(DiaInspection dia, int quarter, DateTime generatedAt) =>
        $"INV-{dia.DiaNumber}-{quarter:D1}-{generatedAt:yyyyMMddHHmmss}";
}

public sealed class GetTechnicianDiaListHandler(
    ITechnicianInspectionRepository repository,
    ITechnicianInspectionCalculator calculator)
    : IRequestHandler<GetTechnicianDiaListQuery, ApiResult<IReadOnlyList<TechnicianDiaListItemDto>>>
{
    public async Task<ApiResult<IReadOnlyList<TechnicianDiaListItemDto>>> Handle(
        GetTechnicianDiaListQuery request,
        CancellationToken cancellationToken)
    {
        var dias = await repository.ActiveDiaInspections
            .OrderByDescending(x => x.ActivatedDate)
            .ThenBy(x => x.DiaNumber)
            .ToListAsync(cancellationToken);

        var diaIds = dias.Select(x => x.Id).ToList();
        var inspections = await repository.Inspections.AsNoTracking()
            .Where(x => diaIds.Contains(x.DiaInspectionId))
            .ToListAsync(cancellationToken);

        var submittedByDia = inspections
            .Where(x => x.Status == TechnicianInspectionStatus.Submitted)
            .GroupBy(x => x.DiaInspectionId)
            .ToDictionary(g => g.Key, g => g.Count());

        var items = dias.Select(dia =>
        {
            var cycle = calculator.Calculate(dia.InspectionStartedDate, submittedByDia.GetValueOrDefault(dia.Id));
            var quarterInspection = cycle.CurrentQuarter is { } q
                ? inspections.FirstOrDefault(x => x.DiaInspectionId == dia.Id && x.Quarter == q)
                : null;

            return new TechnicianDiaListItemDto(
                dia.Id,
                dia.DiaNumber,
                dia.ClientNumber,
                dia.ClientName,
                dia.ClientLocation,
                dia.ActivatedDate,
                cycle.Status,
                cycle.CurrentQuarter,
                TechnicianSupport.ResolveAction(cycle.CurrentQuarter, quarterInspection),
                quarterInspection?.Id);
        }).ToList();

        return ApiResult<IReadOnlyList<TechnicianDiaListItemDto>>.Success(items);
    }
}

public sealed class GetTechnicianDiaHandler(
    ITechnicianInspectionRepository repository,
    ITechnicianInspectionCalculator calculator)
    : IRequestHandler<GetTechnicianDiaQuery, ApiResult<TechnicianDiaDetailDto>>
{
    public async Task<ApiResult<TechnicianDiaDetailDto>> Handle(
        GetTechnicianDiaQuery request,
        CancellationToken cancellationToken)
    {
        var dia = await repository.FindActiveDiaAsync(request.Id, cancellationToken);
        if (dia is null)
            return ApiResult<TechnicianDiaDetailDto>.Failure("Activated DIA inspection not found.");

        var submittedQuarters = await repository.Inspections
            .CountAsync(x => x.DiaInspectionId == dia.Id && x.Status == TechnicianInspectionStatus.Submitted, cancellationToken);
        var cycle = calculator.Calculate(dia.InspectionStartedDate, submittedQuarters);
        TechnicianInspection? quarterInspection = null;
        if (cycle.CurrentQuarter is { } quarter)
        {
            quarterInspection = await repository.FindQuarterInspectionAsync(
                dia.Id, quarter, cancellationToken);
        }

        var action = TechnicianSupport.ResolveAction(cycle.CurrentQuarter, quarterInspection);

        return ApiResult<TechnicianDiaDetailDto>.Success(new(
            dia.Id,
            dia.DiaNumber,
            dia.ClientNumber,
            dia.ClientName,
            dia.ClientLocation,
            dia.ActivatedDate,
            dia.InspectionStartedDate,
            cycle.Status,
            cycle.CurrentQuarter,
            cycle.QuarterStartDate,
            cycle.QuarterEndDate,
            cycle.RemainingDays,
            cycle.ProgressPercent,
            action,
            quarterInspection?.Id,
            quarterInspection is null ? null : TechnicianSupport.ToDto(quarterInspection)));
    }
}

public sealed class GetTechnicianInspectionHandler(
    ITechnicianInspectionRepository repository,
    ICurrentUser actor)
    : IRequestHandler<GetTechnicianInspectionQuery, ApiResult<TechnicianInspectionDto>>
{
    public async Task<ApiResult<TechnicianInspectionDto>> Handle(
        GetTechnicianInspectionQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await repository.FindInspectionAsync(request.Id, cancellationToken);
        if (entity is null || entity.TechnicianId != actor.UserId)
            return ApiResult<TechnicianInspectionDto>.Failure("Inspection not found.");

        return ApiResult<TechnicianInspectionDto>.Success(TechnicianSupport.ToDto(entity));
    }
}

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
        var cycle = calculator.Calculate(dia.InspectionStartedDate, submittedQuarters);
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

public sealed class SaveTechnicianInspectionDraftHandler(
    ITechnicianInspectionRepository repository,
    ICurrentUser actor,
    TimeProvider timeProvider)
    : IRequestHandler<SaveTechnicianInspectionDraftCommand, ApiResult<TechnicianInspectionDto>>
{
    public async Task<ApiResult<TechnicianInspectionDto>> Handle(
        SaveTechnicianInspectionDraftCommand request,
        CancellationToken cancellationToken)
    {
        // Load the inspection WITHOUT its child rows: the draft save replaces all of them, and
        // tracking the old rows is what caused stale-row concurrency failures when a record's
        // child data had been left inconsistent by earlier saves.
        var entity = await repository.FindInspectionForDraftAsync(request.InspectionId, cancellationToken);
        if (entity is null)
            return ApiResult<TechnicianInspectionDto>.Failure("Inspection not found.");

        if (entity.Status == TechnicianInspectionStatus.Submitted)
            return ApiResult<TechnicianInspectionDto>.Failure("Submitted inspections are read only.");

        var before = TechnicianSupport.Snapshot(entity);
        TechnicianSupport.ApplyDraft(entity, request, timeProvider);
        repository.AddHistory(TechnicianSupport.Audit(
            entity, TechnicianInspectionAction.SaveDraft, actor, before, TechnicianSupport.Snapshot(entity)));

        // Atomically drop any existing child rows (including duplicates/orphans left by earlier
        // saves) and persist the freshly built ones. Being set-based, this cannot hit the
        // "row not found" concurrency error the change-tracker delete path was prone to.
        await repository.ReplaceInspectionDetailsAsync(entity, cancellationToken);

        return ApiResult<TechnicianInspectionDto>.Success(TechnicianSupport.ToDto(entity));
    }
}

public sealed class SubmitTechnicianInspectionHandler(
    ITechnicianInspectionRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUser actor,
    TimeProvider timeProvider)
    : IRequestHandler<SubmitTechnicianInspectionCommand, ApiResult<TechnicianInspectionDto>>
{
    public async Task<ApiResult<TechnicianInspectionDto>> Handle(
        SubmitTechnicianInspectionCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await repository.FindInspectionAsync(request.InspectionId, cancellationToken);
        if (entity is null)
            return ApiResult<TechnicianInspectionDto>.Failure("Inspection not found.");

        if (entity.Status == TechnicianInspectionStatus.Submitted)
            return ApiResult<TechnicianInspectionDto>.Failure("Inspection is already submitted.");

        var dia = await repository.FindActiveDiaAsync(entity.DiaInspectionId, cancellationToken);
        if (dia is null)
            return ApiResult<TechnicianInspectionDto>.Failure("Activated DIA inspection not found.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var before = TechnicianSupport.Snapshot(entity);
        entity.Status = TechnicianInspectionStatus.Submitted;
        entity.SubmittedAt = now;
        entity.UpdatedAt = now;

        var invoiceNumber = TechnicianSupport.GenerateInvoiceNumber(dia, entity.Quarter, now);
        repository.AddInvoice(new InspectionInvoice
        {
            Id = Guid.CreateVersion7(),
            TechnicianInspectionId = entity.Id,
            DiaInspectionId = dia.Id,
            Quarter = entity.Quarter,
            InvoiceNumber = invoiceNumber,
            GeneratedAt = now,
            CreatedAt = now,
        });

        repository.AddHistory(TechnicianSupport.Audit(
            entity, TechnicianInspectionAction.Submit, actor, before, TechnicianSupport.Snapshot(entity)));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResult<TechnicianInspectionDto>.Success(TechnicianSupport.ToDto(entity));
    }
}

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

public sealed class GetTechnicianHistoryHandler(ITechnicianInspectionRepository repository)
    : IRequestHandler<GetTechnicianHistoryQuery, ApiResult<PaginatedResult<TechnicianInspectionHistoryDto>>>
{
    public async Task<ApiResult<PaginatedResult<TechnicianInspectionHistoryDto>>> Handle(
        GetTechnicianHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.PageNumber);
        var size = Math.Clamp(request.PageSize, 1, 100);
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

public sealed class GetTechnicianInvoicesHandler(ITechnicianInspectionRepository repository)
    : IRequestHandler<GetTechnicianInvoicesQuery, ApiResult<IReadOnlyList<InspectionInvoiceDto>>>
{
    public async Task<ApiResult<IReadOnlyList<InspectionInvoiceDto>>> Handle(
        GetTechnicianInvoicesQuery request,
        CancellationToken cancellationToken)
    {
        var query = repository.Invoices;
        if (request.DiaId is { } diaId)
            query = query.Where(x => x.DiaInspectionId == diaId);

        var items = await query
            .Join(repository.ActiveDiaInspections,
                invoice => invoice.DiaInspectionId,
                dia => dia.Id,
                (invoice, dia) => new { invoice, dia.DiaNumber })
            .OrderByDescending(x => x.invoice.GeneratedAt)
            .Select(x => new InspectionInvoiceDto(
                x.invoice.Id,
                x.invoice.TechnicianInspectionId,
                x.invoice.DiaInspectionId,
                x.DiaNumber,
                x.invoice.Quarter,
                x.invoice.InvoiceNumber,
                x.invoice.GeneratedAt))
            .ToListAsync(cancellationToken);

        return ApiResult<IReadOnlyList<InspectionInvoiceDto>>.Success(items);
    }
}

public sealed class GetTechnicianFinalSummaryHandler(
    ITechnicianInspectionRepository repository,
    ITechnicianInspectionCalculator calculator)
    : IRequestHandler<GetTechnicianFinalSummaryQuery, ApiResult<TechnicianFinalSummaryDto>>
{
    public async Task<ApiResult<TechnicianFinalSummaryDto>> Handle(
        GetTechnicianFinalSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var dia = await repository.FindActiveDiaAsync(request.DiaInspectionId, cancellationToken);
        if (dia is null)
            return ApiResult<TechnicianFinalSummaryDto>.Failure("Activated DIA inspection not found.");

        var inspections = await repository.Inspections.AsNoTracking()
            .Include(x => x.Cameras)
            .Include(x => x.Network)
            .Include(x => x.Vms)
            .Include(x => x.UpsGeneral)
            .Include(x => x.Anpr)
            .Include(x => x.Kpoi)
            .Where(x => x.DiaInspectionId == dia.Id)
            .OrderBy(x => x.Quarter)
            .ToListAsync(cancellationToken);

        var submittedQuarters = inspections.Count(x => x.Status == TechnicianInspectionStatus.Submitted);
        var cycle = calculator.Calculate(dia.InspectionStartedDate, submittedQuarters);
        if (cycle.Status != TechnicianInspectionCycleStatus.Completed)
            return ApiResult<TechnicianFinalSummaryDto>.Failure("Final summary is available after the inspection cycle completes.");

        var invoices = await repository.Invoices
            .Where(x => x.DiaInspectionId == dia.Id)
            .OrderBy(x => x.Quarter)
            .Select(x => new InspectionInvoiceDto(
                x.Id, x.TechnicianInspectionId, x.DiaInspectionId,
                dia.DiaNumber, x.Quarter, x.InvoiceNumber, x.GeneratedAt))
            .ToListAsync(cancellationToken);

        var history = await repository.History
            .Where(x => x.DiaInspectionId == dia.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new TechnicianInspectionHistoryDto(
                x.Id, x.TechnicianInspectionId, x.DiaInspectionId,
                x.Action.ToString(), x.ActorId, x.ActorName,
                x.CreatedAt, x.BeforeJson, x.AfterJson))
            .ToListAsync(cancellationToken);

        return ApiResult<TechnicianFinalSummaryDto>.Success(new(
            dia.Id,
            dia.DiaNumber,
            dia.ClientName,
            dia.InspectionStartedDate,
            inspections.Select(TechnicianSupport.ToDto).ToList(),
            invoices,
            history));
    }
}
