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

    /// <summary>Sum of every line. Server-computed on each write.</summary>
    public decimal Total { get; set; }

    public ICollection<BoqSection> Sections { get; set; } = [];
}
