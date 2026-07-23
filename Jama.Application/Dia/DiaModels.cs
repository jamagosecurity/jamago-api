using AutoMapper;
using Jama.Domain.Entities;

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
    public DateTime CreatedDate { get; init; }
    public DateTime? ActivatedDate { get; init; }
    public bool IsActive { get; init; }
    public DiaStatus Status { get; init; }
    public int? CurrentQuarter { get; init; }
    public DateTime? QuarterStartDate { get; init; }
    public DateTime? QuarterEndDate { get; init; }
    public DateTime? NextInspectionDate { get; init; }
    public int RemainingDays { get; init; }
    public decimal ProgressPercent { get; init; }
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
    decimal ProgressPercent);

public interface IDiaInspectionCalculator
{
    /// <summary>
    /// Computes the admin-facing status of a DIA. The active quarter tracks how many quarterly
    /// technician inspections have actually been <paramref name="submittedQuarters"/> (same rule as
    /// the technician portal) rather than elapsed calendar time, so both views stay in sync.
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

        var quarter = submitted + 1;
        var start = activated.AddMonths((quarter - 1) * 3);
        var end = activated.AddMonths(quarter * 3);
        var remaining = Math.Max(0, (int)Math.Ceiling((end - now).TotalDays));

        return new((DiaStatus)quarter, quarter, start, end, end, remaining, quarter * 25);
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
            .ForMember(d => d.CreatedBy, o => o.Ignore())
            .ForMember(d => d.UpdatedBy, o => o.Ignore());
    }
}
