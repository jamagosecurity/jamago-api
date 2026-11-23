namespace Jama.Domain.Entities;

/// <summary>Someone who came on the trip with the claimant, and whether they ate the site lunch.</summary>
public class TravelClaimPerson : BaseEntity
{
    public Guid TravelClaimId { get; set; }
    public TravelClaim TravelClaim { get; set; } = null!;
    public Guid PersonId { get; set; }
    public string PersonName { get; set; } = string.Empty;
    public bool AteLunch { get; set; }
}
