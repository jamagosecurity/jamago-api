using Jama.Domain.Enums;

namespace Jama.Domain.Entities;

/// <summary>
/// A CAD drawing submitted for approval — its own document, not a quotation
/// line or an attachment on one.
///
/// A drawing rarely renders on its own: DWG and DXF need AutoCAD to open, so
/// the workflow expects a plotted PDF alongside the native file — the PDF is
/// what an approver looks at, the DWG is what gets downloaded to edit. See
/// DrawingFile.
/// </summary>
public class Drawing : BaseEntity
{
    /// <summary>Reference such as "DRW-00001". Assigned once, on creation, and
    /// never reused even if the drawing is later deleted.</summary>
    public string DrawingNumber { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public string? SiteLocation { get; set; }
    public string? ContactNumber { get; set; }
    public string? Notes { get; set; }

    public DrawingStatus Status { get; set; } = DrawingStatus.Draft;

    /// <summary>The staff account that put it together, for the document footer
    /// and so an administrator can see who to ask about it.</summary>
    public Guid PreparedById { get; set; }
    public string? PreparedByName { get; set; }

    // ===== Where the approval stands =====
    //
    // Denormalised from the history rows, on the same terms as Boq — a list of
    // approved drawings has to show who approved each without a join and a
    // "latest of" per row. The trail below is the record of what happened;
    // these are the answer to where it landed.
    //
    // A decision is CLEARED when the drawing moves on — re-submitting a
    // rejected drawing drops the rejection from here, because it is no longer
    // where the document stands. It stays in the history.

    public DateTime? SubmittedAt { get; set; }

    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Guid? RejectedById { get; set; }
    public string? RejectedByName { get; set; }
    public DateTime? RejectedAt { get; set; }

    /// <summary>Why the current rejection was given. Required to reject.</summary>
    public string? RejectionReason { get; set; }

    public ICollection<DrawingFile> Files { get; set; } = [];

    /// <summary>Every approval step, oldest first. Append-only.</summary>
    public ICollection<DrawingApprovalEvent> ApprovalEvents { get; set; } = [];
}
