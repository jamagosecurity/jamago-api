using Jama.Domain.Enums;

namespace Jama.Domain.Entities;

/// <summary>One step in a claim's history — who, when, and anything said. Only ever added, never changed.</summary>
public class TravelClaimEvent : BaseEntity
{
    public Guid TravelClaimId { get; set; }
    public TravelClaim TravelClaim { get; set; } = null!;
    public TravelClaimAction Action { get; set; }
    public Guid ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public decimal? Amount { get; set; }
}
