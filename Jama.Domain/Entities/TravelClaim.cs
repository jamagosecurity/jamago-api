using Jama.Domain.Enums;

namespace Jama.Domain.Entities;

/// <summary>
/// An Uber bill a staff member paid from their own account and claims back. The claim is the header — who, which trip,
/// where it stands; what is paid back sits on its <see cref="Lines"/> (the ride, and lunch on a site trip), each with
/// its own receipt. Every step is written to <see cref="Events"/>, and the payout is a <see cref="Reimbursement"/>,
/// so it is always clear who did what, when, and how long the money was owed. Names are copied so the record reads
/// the same if someone is renamed or leaves.
/// </summary>
public class TravelClaim : BaseEntity
{
    /// <summary>"UB-00001".</summary>
    public string ClaimNumber { get; set; } = string.Empty;

    public Guid ClaimantId { get; set; }
    public string ClaimantName { get; set; } = string.Empty;

    /// <summary>The Doha day and time of the trip.</summary>
    public DateOnly TripOn { get; set; }
    public TimeOnly? TripTime { get; set; }
    public TravelTripType TripType { get; set; }
    public string? SiteName { get; set; }
    public string? Pickup { get; set; }
    public string? Dropoff { get; set; }

    /// <summary>Whether the claimant was one of the people who ate the site lunch.</summary>
    public bool ClaimantAteLunch { get; set; }

    /// <summary>What was claimed: the sum of the lines, as read from the receipts.</summary>
    public decimal Total { get; set; }

    /// <summary>What the accountant approved — the claimed <see cref="Total"/> or less, never more. Null until approved.
    /// This is what is owed and what is paid back.</summary>
    public decimal? ApprovedAmount { get; set; }

    /// <summary>Why the approved amount is lower than claimed. Required whenever it is.</summary>
    public string? AdjustmentReason { get; set; }
    public string? Note { get; set; }

    public TravelClaimStatus Status { get; set; } = TravelClaimStatus.Submitted;
    public DateTime SubmittedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }

    public DateTime? RejectedAt { get; set; }
    public Guid? RejectedById { get; set; }
    public string? RejectedByName { get; set; }
    public string? RejectReason { get; set; }

    public DateTime? WithdrawnAt { get; set; }

    /// <summary>The payout that settled this claim; set when it is paid back.</summary>
    public Guid? ReimbursementId { get; set; }
    public Reimbursement? Reimbursement { get; set; }

    /// <summary>Optimistic concurrency: two people acting on the same claim at once cannot both succeed.</summary>
    public uint Version { get; set; }

    public List<TravelClaimLine> Lines { get; set; } = [];
    public List<TravelClaimPerson> People { get; set; } = [];
    public List<TravelClaimEvent> Events { get; set; } = [];
}
