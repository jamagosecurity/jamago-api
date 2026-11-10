using Jama.Domain.Enums;

namespace Jama.Domain.Entities;

/// <summary>
/// A paid service callout — camera, cabling, NVR, or software trouble at a
/// site — logged either by the client it happened to or by staff on their
/// behalf. Separate from DiaInspection on purpose: DIA is the Ministry of
/// Interior's regulated quarterly inspection cycle, and this is ordinary
/// non-MOI service work with no regulatory shape to it at all.
/// </summary>
public class MaintenanceRequest : BaseEntity
{
    /// <summary>Human reference, e.g. "MNT-00007". Unique.</summary>
    public string RequestNumber { get; set; } = string.Empty;

    /// <summary>The issues this visit covers, each with its own category,
    /// quantity and fee — see MaintenanceRequestLine.</summary>
    public ICollection<MaintenanceRequestLine> Lines { get; set; } = new List<MaintenanceRequestLine>();

    public string? Description { get; set; }
    public string? SiteLocation { get; set; }
    public string? ClientName { get; set; }

    /// <summary>Required on every ticket — the address the client is reached
    /// at about this job.</summary>
    public string ClientEmail { get; set; } = string.Empty;
    public string? ContactNumber { get; set; }

    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Open;

    /// <summary>Sum of every line, before any discount. Server-computed on each
    /// write.</summary>
    public decimal Subtotal { get; set; }

    /// <summary>A lump sum taken off the finished ticket, in QAR — held as an
    /// amount beside the subtotal rather than folded into the line fees, the
    /// same way a quotation's SpecialDiscount is, so what was given away stays
    /// visible. Only maintenance.manage may set it.</summary>
    public decimal Discount { get; set; }

    /// <summary>What the client pays: <see cref="Subtotal"/> less
    /// <see cref="Discount"/>. Server-computed on each write, and stored so the
    /// queue can show it without loading every line.</summary>
    public decimal ServiceFee { get; set; }

    /// <summary>Whoever filed it — a client reporting their own issue, or
    /// staff logging a phone-in report. Either way, an AdminUser id.</summary>
    public Guid ReportedById { get; set; }
    public string? ReportedByName { get; set; }

    public Guid? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime? AssignedAt { get; set; }

    public string? ResolutionNotes { get; set; }
    public Guid? ResolvedById { get; set; }
    public string? ResolvedByName { get; set; }
    public DateTime? ResolvedAt { get; set; }

    /// <summary>Why it was withdrawn. Set only when Status is Cancelled.</summary>
    public string? CancellationReason { get; set; }
}
