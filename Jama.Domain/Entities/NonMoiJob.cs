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

    /// <summary>The client's email, so the bill can be sent when the deal is closed. Optional: it can also be typed at that point.</summary>
    public string? CustomerEmail { get; set; }

    /// <summary>Who issued the job. Only this person (and administrators) can close the deal.
    /// Empty for jobs issued before this was recorded — those can be closed by an administrator only.</summary>
    public Guid AssignedById { get; set; }
    public string AssignedByName { get; set; } = string.Empty;

    /// <summary>Where the job is: "Assigned" (with the technician) → "Completed" (he has finished; waiting for the issuer)
    /// → "Sent" (the bill is with the client for approval) → "Closed" (the client approved — final), or
    /// "ChangesRequested" (the client rejected or commented; back to the issuer to adjust and send again).</summary>
    public string Status { get; set; } = "Assigned";
    public DateTime? CompletedAt { get; set; }

    /// <summary>Sum of the work lines, from the Non-MOI price list at the moment of completion.</summary>
    public decimal WorkTotal { get; set; }
    public string? CompletionNotes { get; set; }

    // ===== Closing the deal =====

    /// <summary>A lump sum off <see cref="WorkTotal"/>, in QAR. Never more than the total. Prices and items are never editable.</summary>
    public decimal Discount { get; set; }

    /// <summary>What the client pays: <see cref="WorkTotal"/> less <see cref="Discount"/>. Set when the deal is closed.</summary>
    public decimal AmountPayable { get; set; }

    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedById { get; set; }
    public string? ClosedByName { get; set; }

    /// <summary>When the bill email / WhatsApp message went out. Kept separately so a retry after one failed
    /// never sends the channel that already worked a second time.</summary>
    public DateTime? EmailSentAt { get; set; }
    public DateTime? WhatsAppSentAt { get; set; }
    public string? SentFromAccount { get; set; }

    /// <summary>An unguessable token that makes the bill's PDF link work for the client without signing in.</summary>
    public string? BillToken { get; set; }

    /// <summary>Which version of the bill the client is looking at. Starts at 1; sending again after the client asked for
    /// changes makes it 2, 3, and so on. Shown on the bill and the client's page once it is above 1.</summary>
    public int BillRevision { get; set; } = 1;

    // ===== What the client answered =====

    /// <summary>"Approved", "Rejected" or "Commented" — the client's latest answer on the current revision.</summary>
    public string? ClientDecision { get; set; }
    public string? ClientMessage { get; set; }
    public string? ClientResponderName { get; set; }
    public DateTime? ClientRespondedAt { get; set; }

    /// <summary>Every step of the bill's life — sent, approved, rejected, commented, sent again — oldest first.</summary>
    public ICollection<NonMoiJobEvent> Events { get; set; } = [];

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

/// <summary>One step in a job's bill history. Append-only: nothing here is ever edited.</summary>
public class NonMoiJobEvent : BaseEntity
{
    public Guid NonMoiJobId { get; set; }
    public NonMoiJob NonMoiJob { get; set; } = null!;

    /// <summary>"Sent", "Approved", "Rejected" or "Commented".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Who did it: a staff member's name, or the client's.</summary>
    public string ActorName { get; set; } = string.Empty;

    public string? Note { get; set; }
    public int Revision { get; set; } = 1;
    public decimal AmountPayable { get; set; }
}
