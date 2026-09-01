using Jama.Domain.Enums;

namespace Jama.Application.Boqs;

public sealed record BoqLineDto(
    Guid Id,
    Guid? CameraId,
    /// <summary>Reader-facing number, e.g. "1.2". Derived from position, never stored.</summary>
    string Number,
    string ItemName,
    string? ModelNo,
    string? Brand,
    /// <summary>Form factor as the catalogue held it when the line was written.</summary>
    string? Type,
    UnitOfMeasurement Uom,
    decimal Quantity,
    /// <summary>The recording profile as it stood when this line was written, not
    /// as the catalogue holds it now. Unspecified and null on everything that is
    /// not a camera.</summary>
    CameraResolution Resolution,
    decimal? BitrateMbps,
    decimal UnitRate,
    decimal LineTotal,
    int SortOrder);

public sealed record BoqSectionDto(
    Guid Id,
    string Title,
    int SortOrder,
    decimal Subtotal,
    IReadOnlyList<BoqLineDto> Lines);

public sealed record BoqDto(
    Guid Id,
    string BoqNumber,
    string ProjectName,
    string? SiteLocation,
    string? ClientName,
    string? ContactNumber,
    DateOnly IssueDate,
    BoqStatus Status,
    string? Notes,
    Guid PreparedById,
    string? PreparedByName,
    decimal Total,
    IReadOnlyList<BoqSectionDto> Sections,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Row shape for the list — sections and lines are not shown there.</summary>
public sealed record BoqListItemDto(
    Guid Id,
    string BoqNumber,
    string ProjectName,
    string? SiteLocation,
    string? ClientName,
    DateOnly IssueDate,
    BoqStatus Status,
    decimal Total,
    int SectionCount,
    int LineCount,
    string? PreparedByName,
    DateTime CreatedAt);
