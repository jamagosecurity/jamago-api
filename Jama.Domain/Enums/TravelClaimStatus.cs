namespace Jama.Domain.Enums;

/// <summary>Where an Uber bill is: Submitted → Approved → Paid, or Rejected, or Withdrawn by the person before a decision.</summary>
public enum TravelClaimStatus
{
    Submitted,
    Approved,
    Rejected,
    Paid,
    Withdrawn,
}
