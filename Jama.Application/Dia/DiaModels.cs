using AutoMapper;
using Jama.Domain.Entities;using Jama.Domain.Enums;

namespace Jama.Application.Dia;

public enum DiaStatus
{
    Inactive,
    Quarter1,
    Quarter2,
    Quarter3,
    Quarter4,
    Completed,
}

public sealed record DiaInspectionDto
{
    public Guid Id { get; init; }
    public string DiaNumber { get; init; } = string.Empty;
    public string ClientNumber { get; init; } = string.Empty;
    public string ClientName { get; init; } = string.Empty;
    public string ClientLocation { get; init; } = string.Empty;
    /// <summary>WGS 84 site pin. Null when the site has not been pinned; never set alone.</summary>
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? ActivatedDate { get; init; }
    public bool IsActive { get; init; }
    /// <summary>Soft-deleted. Only ever true when listing with Archived=true.</summary>
    public bool IsArchived { get; init; }
    public DiaStatus Status { get; init; }
    public int? CurrentQuarter { get; init; }
    public DateTime? QuarterStartDate { get; init; }
    public DateTime? QuarterEndDate { get; init; }
    public DateTime? NextInspectionDate { get; init; }
    public int RemainingDays { get; init; }
    public decimal ProgressPercent { get; init; }
    /// <summary>Quarter windows closed with nothing submitted against them.</summary>
    public int OverdueQuarters { get; init; }
    public string? CreatedBy { get; init; }
    public string? UpdatedBy { get; init; }
    public DateTime? UpdatedDate { get; init; }
}

public sealed record DiaDashboardDto(
    int Total,
    int Active,
    int Inactive,
    int Quarter1,
    int Quarter2,
    int Quarter3,
    int Quarter4,
    int Completed);

public sealed record DiaInspectionHistoryDto(
    Guid Id,
    Guid DiaInspectionId,
    DiaInspectionAction Action,
    Guid ActorId,
    string? ActorName,
    DateTime CreatedDate,
    string? BeforeJson,
    string? AfterJson);

public sealed record DiaCalculation(
    DiaStatus Status,
    int? CurrentQuarter,
    DateTime? QuarterStartDate,
    DateTime? QuarterEndDate,
    DateTime? NextInspectionDate,
    int RemainingDays,
    decimal ProgressPercent,
    /// <summary>
    /// Quarter windows that have closed with nothing submitted against them. Never
    /// counted as done — this is the count of inspections that were missed.
    /// </summary>
    int OverdueQuarters = 0);

public interface IDiaInspectionCalculator
{
    /// <summary>
    /// Computes the admin-facing status of a DIA.
    ///
    /// The active quarter follows the calendar: quarter windows are three months long from the
    /// activation date, so a site activated in December is in its third quarter by August whether
    /// or not anyone inspected it. Windows that closed with nothing submitted are reported as
    /// <see cref="DiaCalculation.OverdueQuarters"/> — never as progress.
    ///
    /// This used to count submitted inspections instead, which kept a site that had been running
    /// for eight months displaying "Quarter 1" and made the register unable to show that
    /// inspections had been missed at all. A technician working ahead still moves on: the quarter
    /// is whichever is further along, calendar or submissions.
    /// </summary>
    DiaCalculation Calculate(bool isActive, DateTime? activatedDate, int submittedQuarters);
}

public sealed class DiaInspectionCalculator(TimeProvider timeProvider) : IDiaInspectionCalculator
{
    public DiaCalculation Calculate(bool isActive, DateTime? activatedDate, int submittedQuarters)
    {
        if (!isActive || activatedDate is null)
            return new(DiaStatus.Inactive, null, null, null, null, 0, 0);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var activated = DateTime.SpecifyKind(activatedDate.Value, DateTimeKind.Utc);
        if (now < activated)
            return new(DiaStatus.Inactive, null, null, null, null, 0, 0);

        var submitted = Math.Clamp(submittedQuarters, 0, 4);
        if (submitted >= 4)
        {
            var completion = activated.AddMonths(12);
            return new(DiaStatus.Completed, null, activated, completion, null, 0, 100);
        }

        // Windows that have fully closed. The year has four, so a site older than
        // twelve months stops at four rather than running off the end.
        var elapsed = 0;
        while (elapsed < 4 && activated.AddMonths((elapsed + 1) * 3) <= now)
        {
            elapsed++;
        }

        // Whichever is further along: the calendar, or a technician working ahead.
        var quarter = Math.Clamp(Math.Max(submitted + 1, elapsed + 1), 1, 4);
        var overdue = Math.Max(0, elapsed - submitted);

        var start = activated.AddMonths((quarter - 1) * 3);
        var end = activated.AddMonths(quarter * 3);
        var remaining = Math.Max(0, (int)Math.Ceiling((end - now).TotalDays));

        // Progress stays a measure of work done, not time served, so a site with
        // three missed quarters still reads 0%.
        var progress = submitted * 25m;

        return new((DiaStatus)quarter, quarter, start, end, end, remaining, progress, overdue);
    }
}

public sealed class DiaMappingProfile : Profile
{
    public DiaMappingProfile()
    {
        CreateMap<DiaInspection, DiaInspectionDto>()
            .ForMember(d => d.CreatedDate, o => o.MapFrom(s => s.CreatedAt))
            .ForMember(d => d.UpdatedDate, o => o.MapFrom(s => s.UpdatedAt))
            .ForMember(d => d.Status, o => o.Ignore())
            .ForMember(d => d.CurrentQuarter, o => o.Ignore())
            .ForMember(d => d.QuarterStartDate, o => o.Ignore())
            .ForMember(d => d.QuarterEndDate, o => o.Ignore())
            .ForMember(d => d.NextInspectionDate, o => o.Ignore())
            .ForMember(d => d.RemainingDays, o => o.Ignore())
            .ForMember(d => d.ProgressPercent, o => o.Ignore())
            .ForMember(d => d.OverdueQuarters, o => o.Ignore())
            .ForMember(d => d.CreatedBy, o => o.Ignore())
            .ForMember(d => d.UpdatedBy, o => o.Ignore());
    }
}
