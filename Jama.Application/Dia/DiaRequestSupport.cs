using System.Text.Json;
using AutoMapper;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Jama.Domain.Enums;

namespace Jama.Application.Dia;

/// <summary>
/// Shared helpers for the DIA handlers: DTO projection, audit rows and the
/// DIA-number normalisation used for uniqueness checks. Feature-level rather
/// than per-use-case because every command needs the same audit shape, and a
/// snapshot that drifted between handlers would corrupt the history trail.
/// </summary>
internal static class DiaRequestSupport
{
    public static string Normalize(string value) => value.Trim().ToUpperInvariant();

    public static object Snapshot(DiaInspection x) => new
    {
        x.Id, x.DiaNumber, x.ClientNumber, x.ClientName, x.ClientLocation,
        x.Latitude, x.Longitude,
        x.ActivatedDate, x.IsActive, x.IsArchived, x.CreatedById, x.UpdatedById,
        x.CreatedAt, x.UpdatedAt,
    };

    public static string Json(object value) => JsonSerializer.Serialize(value);

    public static DiaInspectionDto ToDto(
        DiaInspection entity,
        IDiaInspectionCalculator calculator,
        IMapper mapper,
        int submittedQuarters,
        string? createdBy = null,
        string? updatedBy = null)
    {
        var calculation = calculator.Calculate(entity.IsActive, entity.ActivatedDate, submittedQuarters);
        return mapper.Map<DiaInspectionDto>(entity) with
        {
            Status = calculation.Status,
            CurrentQuarter = calculation.CurrentQuarter,
            QuarterStartDate = calculation.QuarterStartDate,
            QuarterEndDate = calculation.QuarterEndDate,
            NextInspectionDate = calculation.NextInspectionDate,
            RemainingDays = calculation.RemainingDays,
            ProgressPercent = calculation.ProgressPercent,
            OverdueQuarters = calculation.OverdueQuarters,
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
