using Jama.Domain.Entities;using Jama.Domain.Enums;

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
    decimal ProgressPercent,
    /// <summary>Closed windows with nothing submitted. Missed work, not progress.</summary>
    int OverdueQuarters = 0);

public interface ITechnicianInspectionCalculator
{
    /// <summary>
    /// Calculates the technician's current position in the 4-quarter cycle.
    ///
    /// The cycle is anchored on the DIA's schedule, so callers pass the inspection start date
    /// falling back to the activation date: the quarterly clock belongs to the site's MOI
    /// schedule and starts when an administrator activates it, not when a technician first
    /// opens the record. Quarters run three months from that anchor.
    ///
    /// A window that closes with nothing submitted is counted in
    /// <see cref="TechnicianCycleCalculation.OverdueQuarters"/> and never as progress. A
    /// technician working ahead still unlocks the next quarter immediately.
    /// </summary>
    TechnicianCycleCalculation Calculate(DateTime? scheduleAnchor, int submittedQuarters);
}

public sealed class TechnicianInspectionCalculator(TimeProvider timeProvider) : ITechnicianInspectionCalculator
{
    public TechnicianCycleCalculation Calculate(DateTime? scheduleAnchor, int submittedQuarters)
    {
        if (scheduleAnchor is null)
            return new(TechnicianInspectionCycleStatus.NotStarted, null, null, null, 0, 0);

        var started = DateTime.SpecifyKind(scheduleAnchor.Value, DateTimeKind.Utc);
        var submitted = Math.Clamp(submittedQuarters, 0, 4);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (submitted >= 4)
        {
            var completion = started.AddMonths(12);
            return new(TechnicianInspectionCycleStatus.Completed, null, started, completion, 0, 100);
        }

        // A schedule that has not begun yet has no active quarter to work on.
        if (now < started)
            return new(TechnicianInspectionCycleStatus.NotStarted, null, null, null, 0, 0);

        var elapsed = 0;
        while (elapsed < 4 && started.AddMonths((elapsed + 1) * 3) <= now)
        {
            elapsed++;
        }

        var quarter = Math.Clamp(Math.Max(submitted + 1, elapsed + 1), 1, 4);
        var overdue = Math.Max(0, elapsed - submitted);

        var start = started.AddMonths((quarter - 1) * 3);
        var end = started.AddMonths(quarter * 3);
        var remaining = Math.Max(0, (int)Math.Ceiling((end - now).TotalDays));

        return new(
            (TechnicianInspectionCycleStatus)quarter,
            quarter,
            start,
            end,
            remaining,
            submitted * 25m,
            overdue);
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
    // Site pin, so the card can offer turn-by-turn navigation instead of an
    // address the technician has to retype into a maps app. Null when unpinned.
    double? Latitude,
    double? Longitude,
    DateTime? ActivatedDate,
    TechnicianInspectionCycleStatus InspectionStatus,
    int? CurrentQuarter,
    TechnicianDiaAction Action,
    Guid? CurrentInspectionId,
    // Cycle progress. The list handler already computes the full calculation to
    // resolve status; these carry the rest of it so the dashboard can show
    // quarters done/left, the quarter window and time remaining without a
    // per-row detail call.
    int SubmittedQuarters,
    DateTime? QuarterStartDate,
    DateTime? QuarterEndDate,
    int RemainingDays,
    decimal ProgressPercent,
    /// <summary>Quarter windows that closed with nothing submitted, so the card can
    /// say a site is behind rather than only which quarter is due.</summary>
    int OverdueQuarters = 0,
    /// <summary>
    /// The date each of the four quarters opens, oldest first — the same four dates
    /// the MOI workflow sheet lists per site. The card previously showed only the
    /// current quarter's window, so a technician could not see when Q3 or Q4 fall
    /// without opening the record. Derived here rather than in the browser so the
    /// three-month rule has one home.
    /// </summary>
    IReadOnlyList<DateTime>? QuarterDates = null);

public sealed record TechnicianDiaDetailDto(
    Guid Id,
    string DiaNumber,
    string ClientNumber,
    string ClientName,
    string ClientLocation,
    double? Latitude,
    double? Longitude,
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
