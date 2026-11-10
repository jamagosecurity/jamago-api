namespace Jama.Domain.Entities;

/// <summary>
/// A Non-MOI job admin hands to one technician: who the customer is, where the
/// site is (with an optional map pin), and what needs doing in English and
/// Arabic. The Non-MOI counterpart of an activated DIA record — the technician
/// sees it with a Navigate button — except that it goes to the one technician
/// it is assigned to rather than to all of them.
///
/// Separate from MaintenanceRequest on purpose: a ticket is a priced checklist
/// a client or technician files; this is a work order admin issues.
/// </summary>
public class NonMoiJob : BaseEntity
{
    /// <summary>Human reference, e.g. "NMJ-00007". Unique.</summary>
    public string JobNumber { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerNumber { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;

    /// <summary>Optional site pin; both or neither — the same rule as a DIA site.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string DescriptionEn { get; set; } = string.Empty;
    public string? DescriptionAr { get; set; }

    /// <summary>The technician (an AdminUser id) who sees this job.</summary>
    public Guid AssignedToId { get; set; }
    public string AssignedToName { get; set; } = string.Empty;

    /// <summary>"Assigned" until the technician finishes it, then "Completed".</summary>
    public string Status { get; set; } = "Assigned";
    public DateTime? CompletedAt { get; set; }

    /// <summary>Sum of the work lines, from the Non-MOI price list at the moment of completion.</summary>
    public decimal WorkTotal { get; set; }
    public string? CompletionNotes { get; set; }

    public ICollection<NonMoiJobLine> Lines { get; set; } = [];
}

/// <summary>One issue the technician fixed on a job, priced from the catalogue when he completed it.
/// Prices are copied, so a later change to the price list never rewrites a finished job.</summary>
public class NonMoiJobLine : BaseEntity
{
    public Guid NonMoiJobId { get; set; }
    public NonMoiJob NonMoiJob { get; set; } = null!;

    public string Category { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitFee { get; set; }
    public decimal LineTotal { get; set; }
    public int SortOrder { get; set; }
}
