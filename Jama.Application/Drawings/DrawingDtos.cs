using Jama.Domain.Enums;

namespace Jama.Application.Drawings;

public sealed record DrawingFileDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? UploadedByName,
    DateTime CreatedAt);

/// <summary>
/// One step in a drawing's approval history, as a reader sees it. The actor's
/// name is the one recorded at the time, not the account's name today.
/// </summary>
public sealed record DrawingApprovalEventDto(
    Guid Id,
    DrawingApprovalAction Action,
    Guid ActorId,
    string? ActorName,
    string? Reason,
    DateTime At);

public sealed record DrawingDto(
    Guid Id,
    string DrawingNumber,
    string ProjectName,
    string? ClientName,
    string? SiteLocation,
    string? ContactNumber,
    string? Notes,
    DrawingStatus Status,
    Guid PreparedById,
    string? PreparedByName,
    // ===== Approval =====
    DateTime? SubmittedAt,
    string? ApprovedByName,
    DateTime? ApprovedAt,
    string? RejectedByName,
    DateTime? RejectedAt,
    string? RejectionReason,
    /// <summary>Whether the files and details may still be changed. Sent rather
    /// than derived on the client so the editor and the server cannot disagree.</summary>
    bool IsEditable,
    int RejectionCount,
    int SubmissionCount,
    IReadOnlyList<DrawingFileDto> Files,
    /// <summary>Every step, oldest first. Append-only.</summary>
    IReadOnlyList<DrawingApprovalEventDto> History,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Row shape for the list — files and the trail are not shown there.</summary>
public sealed record DrawingListItemDto(
    Guid Id,
    string DrawingNumber,
    string ProjectName,
    string? ClientName,
    string? ContactNumber,
    DrawingStatus Status,
    string? PreparedByName,
    DateTime? SubmittedAt,
    string? ApprovedByName,
    DateTime? ApprovedAt,
    string? RejectedByName,
    DateTime? RejectedAt,
    string? RejectionReason,
    int FileCount,
    DateTime CreatedAt);
