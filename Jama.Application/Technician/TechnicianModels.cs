using Jama.Domain.Entities;

namespace Jama.Application.Technician;

public enum TechnicianInspectionCycleStatus
{
    NotStarted,
    Quarter1,
    Quarter2,
    Quarter3,
    Quarter4,
    Completed,
}

public sealed record TechnicianCycleCalculation(
    TechnicianInspectionCycleStatus Status,
    int? CurrentQuarter,
    DateTime? QuarterStartDate,
    DateTime? QuarterEndDate,
    int RemainingDays,
    decimal ProgressPercent);

public interface ITechnicianInspectionCalculator
{
    /// <summary>
    /// Calculates the technician's current position in the 4-quarter cycle.
    /// The active quarter is driven by <paramref name="submittedQuarters"/> (how many quarters
    /// have actually been submitted so far), not by elapsed calendar time — a quarter that is
    /// never submitted stays open/overdue instead of being silently skipped once its date window
    /// passes, and a quarter finished early unlocks the next one immediately.
    /// </summary>
    TechnicianCycleCalculation Calculate(DateTime? inspectionStartedDate, int submittedQuarters);
}

public sealed class TechnicianInspectionCalculator(TimeProvider timeProvider) : ITechnicianInspectionCalculator
{
    public TechnicianCycleCalculation Calculate(DateTime? inspectionStartedDate, int submittedQuarters)
    {
        if (inspectionStartedDate is null)
            return new(TechnicianInspectionCycleStatus.NotStarted, null, null, null, 0, 0);

        var started = DateTime.SpecifyKind(inspectionStartedDate.Value, DateTimeKind.Utc);
        var submitted = Math.Clamp(submittedQuarters, 0, 4);

        if (submitted >= 4)
        {
            var completion = started.AddMonths(12);
            return new(TechnicianInspectionCycleStatus.Completed, null, started, completion, 0, 100);
        }

        var quarter = submitted + 1;
        var start = started.AddMonths((quarter - 1) * 3);
        var end = started.AddMonths(quarter * 3);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var remaining = Math.Max(0, (int)Math.Ceiling((end - now).TotalDays));

        return new((TechnicianInspectionCycleStatus)quarter, quarter, start, end, remaining, quarter * 25m);
    }
}

public enum TechnicianDiaAction
{
    StartInspection,
    Continue,
    View,
}

public sealed record TechnicianDiaListItemDto(
    Guid Id,
    string DiaNumber,
    string ClientNumber,
    string ClientName,
    string ClientLocation,
    DateTime? ActivatedDate,
    TechnicianInspectionCycleStatus InspectionStatus,
    int? CurrentQuarter,
    TechnicianDiaAction Action,
    Guid? CurrentInspectionId);

public sealed record TechnicianDiaDetailDto(
    Guid Id,
    string DiaNumber,
    string ClientNumber,
    string ClientName,
    string ClientLocation,
    DateTime? ActivatedDate,
    DateTime? InspectionStartedDate,
    TechnicianInspectionCycleStatus InspectionStatus,
    int? CurrentQuarter,
    DateTime? QuarterStartDate,
    DateTime? QuarterEndDate,
    int RemainingDays,
    decimal ProgressPercent,
    TechnicianDiaAction Action,
    Guid? CurrentInspectionId,
    TechnicianInspectionDto? CurrentInspection);

public sealed record TechnicianInspectionDto(
    Guid Id,
    Guid DiaInspectionId,
    int Quarter,
    TechnicianInspectionStatus Status,
    DateTime? SubmittedAt,
    bool IsReadOnly,
    IReadOnlyList<CameraDetailDto> Cameras,
    NetworkDetailDto? Network,
    VmsDetailDto? Vms,
    UpsGeneralDetailDto? UpsGeneral,
    AnprConfigurationDto? Anpr,
    KpoiDetailDto? Kpoi);

public sealed record CameraDetailDto(
    Guid? Id,
    string Brand,
    string Model,
    int Quantity,
    string? Location,
    string? Remarks);

public sealed record NetworkDetailDto(
    string? SwitchBrand,
    string? SwitchModel,
    string? RouterBrand,
    string? RouterModel,
    string? Firewall,
    string? RackDetails,
    string? NetworkRemarks);

public sealed record VmsDetailDto(
    string? VmsName,
    string? Version,
    string? LicenseDetails,
    string? ServerDetails,
    string? HealthStatus,
    string? Remarks);

public sealed record UpsGeneralDetailDto(
    string? UpsBrand,
    string? UpsCapacity,
    string? BatteryStatus,
    bool GeneratorAvailable,
    string? GeneratorDetails,
    string? GeneralRemarks);

public sealed record AnprConfigurationDto(
    bool AnprInstalled,
    string? CameraDetails,
    string? Configuration,
    string? SoftwareVersion,
    string? Remarks);

public sealed record KpoiDetailDto(
    string? IvdIvss,
    string? KpoiCamera,
    string? Lens,
    string? HardDisc);

public sealed record SaveTechnicianInspectionDraftRequest(
    Guid InspectionId,
    IReadOnlyList<CameraDetailDto> Cameras,
    NetworkDetailDto? Network,
    VmsDetailDto? Vms,
    UpsGeneralDetailDto? UpsGeneral,
    AnprConfigurationDto? Anpr,
    KpoiDetailDto? Kpoi);

public sealed record TechnicianInspectionHistoryDto(
    Guid Id,
    Guid TechnicianInspectionId,
    Guid? DiaInspectionId,
    string Action,
    Guid ActorId,
    string? ActorName,
    DateTime CreatedDate,
    string? BeforeJson,
    string? AfterJson);

public sealed record InspectionInvoiceDto(
    Guid Id,
    Guid TechnicianInspectionId,
    Guid DiaInspectionId,
    string DiaNumber,
    int Quarter,
    string InvoiceNumber,
    DateTime GeneratedAt);

public sealed record TechnicianFinalSummaryDto(
    Guid DiaInspectionId,
    string DiaNumber,
    string ClientName,
    DateTime? InspectionStartedDate,
    IReadOnlyList<TechnicianInspectionDto> Inspections,
    IReadOnlyList<InspectionInvoiceDto> Invoices,
    IReadOnlyList<TechnicianInspectionHistoryDto> History);
