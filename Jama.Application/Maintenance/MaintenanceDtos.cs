using Jama.Domain.Enums;

namespace Jama.Application.Maintenance;

public sealed record MaintenanceLineDto(
    Guid Id,
    string Category,
    string IssueType,
    string? DescriptionEn,
    string? DescriptionAr,
    int Quantity,
    decimal UnitFee,
    decimal CatalogueFee,
    decimal LineTotal);

public sealed record MaintenanceRequestDto(
    Guid Id,
    string RequestNumber,
    IReadOnlyList<MaintenanceLineDto> Lines,
    string? Description,
    string? SiteLocation,
    string? ClientName,
    string ClientEmail,
    string? ContactNumber,
    MaintenanceStatus Status,
    decimal Subtotal,
    decimal Discount,
    decimal ServiceFee,
    Guid ReportedById,
    string? ReportedByName,
    Guid? AssignedToId,
    string? AssignedToName,
    DateTime? AssignedAt,
    string? ResolutionNotes,
    string? ResolvedByName,
    DateTime? ResolvedAt,
    string? CancellationReason,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Row shape for the ticket list — no description/resolution detail,
/// same reasoning as every other ...ListItemDto in this codebase. A ticket can
/// hold several issues, so the row carries a one-line summary of them and the
/// categories they fall under rather than any single issue.</summary>
public sealed record MaintenanceRequestListItemDto(
    Guid Id,
    string RequestNumber,
    string Summary,
    IReadOnlyList<string> Categories,
    int LineCount,
    string? SiteLocation,
    string? ClientName,
    MaintenanceStatus Status,
    decimal ServiceFee,
    string? ReportedByName,
    string? AssignedToName,
    DateTime CreatedAt);

/// <summary>One line as Create and Update receive it. UnitFee is honoured
/// only from maintenance.manage — see MaintenanceLines.Build.</summary>
public sealed record MaintenanceLineInput(
    string Category,
    string? IssueType,
    int Quantity,
    decimal? UnitFee);

/// <summary>One service on the Non-MOI price list — what the checklist shows
/// and what admin edits. See MaintenanceCatalogueItem.</summary>
public sealed record MaintenanceIssueOptionDto(
    Guid Id,
    string Category,
    string IssueType,
    string? DescriptionEn,
    string? DescriptionAr,
    decimal DefaultFee,
    IReadOnlyList<MaintenanceImageDto> Images);

public sealed record MaintenanceImageDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    int SortOrder,
    /// <summary>Relative URL the browser can put straight in an img src.</summary>
    string Url);

/// <summary>A staff account the ticket can be assigned to — active staff
/// holding a login account, the only ones who can later see "assigned to me".</summary>
public sealed record AssignableStaffDto(Guid AdminUserId, string FullName, string? Department);
