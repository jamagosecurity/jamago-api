using System.Text.Json;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using TechnicianInspectionStatus = Jama.Domain.Enums.TechnicianInspectionStatus;

namespace Jama.Application.Technician;

/// <summary>
/// Helpers shared by more than one technician handler. Anything used by a
/// single use case lives in that use case's folder instead — the draft-applying
/// code and the invoice-number format were both moved out on that basis.
/// </summary>
internal static class TechnicianSupport
{
    private static string Json(object value) => JsonSerializer.Serialize(value);

    /// <summary>
    /// The date each of the four quarters opens, counting three months at a time
    /// from the schedule anchor. Null for a site with no schedule yet, so the card
    /// shows bare quarter chips rather than four meaningless dates.
    /// </summary>
    public static IReadOnlyList<DateTime>? QuarterDates(DateTime? scheduleAnchor)
    {
        if (scheduleAnchor is null)
        {
            return null;
        }

        var anchor = DateTime.SpecifyKind(scheduleAnchor.Value, DateTimeKind.Utc);
        return [anchor, anchor.AddMonths(3), anchor.AddMonths(6), anchor.AddMonths(9)];
    }

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
        TechnicianInspectionCycleStatus cycleStatus,
        int? currentQuarter,
        TechnicianInspection? quarterInspection)
    {
        // A finished cycle has no active quarter; surface a View action (the UI links it to the
        // final summary) instead of a Start action that would fail with "cycle is not active".
        if (cycleStatus == TechnicianInspectionCycleStatus.Completed)
            return TechnicianDiaAction.View;

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
}
