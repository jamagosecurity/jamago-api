using Jama.Domain.Enums;

namespace Jama.Domain.Entities;

/// <summary>
/// One thing a claim pays back, with the receipt that proves it. A ride pays the receipt amount. A lunch pays the bill,
/// but never more than <see cref="RatePerPerson"/> × <see cref="Persons"/>; the rate is copied from the day of the trip,
/// so a later rate change never rewrites a claim.
/// </summary>
public class TravelClaimLine : BaseEntity
{
    public Guid TravelClaimId { get; set; }
    public TravelClaim TravelClaim { get; set; } = null!;

    public TravelClaimLineKind Kind { get; set; }

    /// <summary>What the receipt says was paid.</summary>
    public decimal BillAmount { get; set; }
    /// <summary>What is paid back for this line.</summary>
    public decimal Amount { get; set; }

    /// <summary>Lunch: people who ate. Ride: 1.</summary>
    public int Persons { get; set; } = 1;
    public decimal? RatePerPerson { get; set; }

    /// <summary>True when the amount and date came from Uber's PDF unchanged.</summary>
    public bool ReadFromReceipt { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    /// <summary>SHA-256 of the receipt, so the same receipt cannot be claimed twice.</summary>
    public string FileHash { get; set; } = string.Empty;
}
