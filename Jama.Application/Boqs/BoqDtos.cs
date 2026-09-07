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
    /// <summary>The rate this line is priced at — what the client is charged.</summary>
    decimal UnitRate,
    /// <summary>What the catalogue held when the line was written. Equal to
    /// UnitRate unless somebody overrode it, so the editor can show the list
    /// price beside a negotiated one instead of losing it.</summary>
    decimal CatalogueRate,
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
    /// <summary>Sum of the lines, before the discount.</summary>
    decimal Total,
    decimal SpecialDiscount,
    /// <summary>What is payable: the lines less the discount.</summary>
    decimal GrandTotal,
    // ===== Approval =====
    DateTime? SubmittedAt,
    string? ApprovedByName,
    DateTime? ApprovedAt,
    string? RejectedByName,
    DateTime? RejectedAt,
    /// <summary>Why the current rejection was given — what a rework starts from.</summary>
    string? RejectionReason,
    /// <summary>Whether the lines may still be changed. Sent rather than derived
    /// on the client so the editor and the server cannot disagree about it.</summary>
    bool IsEditable,
    /// <summary>How many times this quotation has been sent back. Read straight
    /// off the trail, so "approved at the third attempt" is a fact rather than
    /// something a reader has to count.</summary>
    int RejectionCount,
    /// <summary>How many times it has been submitted for approval.</summary>
    int SubmissionCount,
    /// <summary>Every step, oldest first. Append-only.</summary>
    IReadOnlyList<BoqApprovalEventDto> History,
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
    decimal SpecialDiscount,
    decimal GrandTotal,
    DateTime? SubmittedAt,
    string? ApprovedByName,
    DateTime? ApprovedAt,
    string? RejectedByName,
    DateTime? RejectedAt,
    string? RejectionReason,
    int RejectionCount,
    int SubmissionCount,
    int SectionCount,
    int LineCount,
    string? PreparedByName,
    DateTime CreatedAt);

/// <summary>
/// One step in a quotation's approval history, as a reader sees it.
///
/// The actor's name is the one recorded at the time, not the account's name
/// today — see BoqApprovalEvent.
/// </summary>
public sealed record BoqApprovalEventDto(
    Guid Id,
    BoqApprovalAction Action,
    Guid ActorId,
    string? ActorName,
    string? Reason,
    DateTime At);
