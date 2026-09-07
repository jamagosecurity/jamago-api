using Jama.Domain.Enums;

namespace Jama.Domain.Entities;

/// <summary>
/// A bill of quantities: what a job needs, in what quantities, at what rates.
///
/// Distinct from <see cref="Quotation"/>. A quotation is a priced offer to a
/// named customer; a BOQ is the schedule of materials for a site, grouped into
/// sections (ground floor, car park) the way the work is actually organised.
///
/// Staff assemble these from the stock catalogue. They choose items and
/// quantities — never rates: every line's rate is copied from the catalogue by
/// the server, so what an item costs stays an administrator's decision.
/// </summary>
public class Boq : BaseEntity
{
    /// <summary>Human reference, e.g. "BOQ-2026-0007". Unique.</summary>
    public string BoqNumber { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;
    public string? SiteLocation { get; set; }
    public string? ClientName { get; set; }

    /// <summary>Whoever the client should be called on about this quote. Free
    /// text, not validated as a number: Qatari numbers are written with spaces,
    /// and a client may give an extension or a second line.</summary>
    public string? ContactNumber { get; set; }

    public DateOnly IssueDate { get; set; }
    public BoqStatus Status { get; set; } = BoqStatus.Draft;
    public string? Notes { get; set; }

    /// <summary>The staff account that put it together, for the document footer
    /// and so an administrator can see who to ask about it.</summary>
    public Guid PreparedById { get; set; }
    public string? PreparedByName { get; set; }

    /// <summary>Sum of every line, before any discount. Server-computed on each
    /// write.</summary>
    public decimal Total { get; set; }

    /// <summary>
    /// A lump sum knocked off the finished quotation, in QAR — the discount
    /// agreed with the customer once the lines are settled.
    ///
    /// Held as an amount rather than a percentage because that is how it is
    /// negotiated: a round number off the total, not a rate applied to it. Kept
    /// beside <see cref="Total"/> rather than folded into the line rates, so the
    /// document can show what was given away instead of quietly restating every
    /// price.
    /// </summary>
    public decimal SpecialDiscount { get; set; }

    /// <summary>What the customer pays: <see cref="Total"/> less
    /// <see cref="SpecialDiscount"/>. Server-computed on each write.</summary>
    public decimal GrandTotal { get; set; }

    public ICollection<BoqSection> Sections { get; set; } = [];

    // ===== Where the approval stands =====
    //
    // Denormalised from the history rows deliberately. A list of approved
    // quotations has to show who approved each and when, and reading that back
    // through the trail would be a join and a "latest of" for every row on the
    // page. The trail remains the record of what happened; these are the answer
    // to where it landed.
    //
    // A decision is CLEARED when the quotation moves on — re-submitting a
    // rejected quotation drops the rejection from here, because it is no longer
    // where the document stands. It stays in the history, which is what the
    // history is for.

    /// <summary>When it was last handed to an approver. Null while it is a
    /// draft nobody has submitted.</summary>
    public DateTime? SubmittedAt { get; set; }

    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Guid? RejectedById { get; set; }
    public string? RejectedByName { get; set; }
    public DateTime? RejectedAt { get; set; }

    /// <summary>Why the current rejection was given. Required to reject, and the
    /// thing the person reworking the quotation actually needs.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Every approval step, oldest first. Append-only.</summary>
    public ICollection<BoqApprovalEvent> ApprovalEvents { get; set; } = [];
}
