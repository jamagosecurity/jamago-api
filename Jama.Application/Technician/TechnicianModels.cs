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
    TechnicianCycleCalculation Calculate(DateTime? inspectionStartedDate);
}

public sealed class TechnicianInspectionCalculator(TimeProvider timeProvider) : ITechnicianInspectionCalculator
{
    public TechnicianCycleCalculation Calculate(DateTime? inspectionStartedDate)
    {
        if (inspectionStartedDate is null)
            return new(TechnicianInspectionCycleStatus.NotStarted, null, null, null, 0, 0);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var started = DateTime.SpecifyKind(inspectionStartedDate.Value, DateTimeKind.Utc);
        var completion = started.AddMonths(12);

        if (now >= completion)
            return new(TechnicianInspectionCycleStatus.Completed, null, started, completion, 0, 100);

        var quarter = now < started.AddMonths(3) ? 1
            : now < started.AddMonths(6) ? 2
            : now < started.AddMonths(9) ? 3
            : 4;
        var start = started.AddMonths((quarter - 1) * 3);
        var end = started.AddMonths(quarter * 3);
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

public sealed record KpoiDetailDto(string? Details);

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
