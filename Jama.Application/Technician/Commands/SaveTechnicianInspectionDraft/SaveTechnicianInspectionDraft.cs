using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using MediatR;
using TechnicianInspectionStatus = Jama.Domain.Enums.TechnicianInspectionStatus;

namespace Jama.Application.Technician.Commands.SaveTechnicianInspectionDraft;

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
        DraftWriter.Apply(entity, request, timeProvider);
        repository.AddHistory(TechnicianSupport.Audit(
            entity, TechnicianInspectionAction.SaveDraft, actor, before, TechnicianSupport.Snapshot(entity)));

        // Atomically drop any existing child rows (including duplicates/orphans left by earlier
        // saves) and persist the freshly built ones. Being set-based, this cannot hit the
        // "row not found" concurrency error the change-tracker delete path was prone to.
        await repository.ReplaceInspectionDetailsAsync(entity, cancellationToken);

        return ApiResult<TechnicianInspectionDto>.Success(TechnicianSupport.ToDto(entity));
    }
}

/// <summary>
/// Copies a draft request onto the entity graph. Lives here rather than in the
/// shared support class because this is the only use case that writes these
/// sections, and having it alongside the command keeps the shape of the request
/// and the shape of the write in one place.
/// </summary>
internal static class DraftWriter
{
    public static void Apply(
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
}
