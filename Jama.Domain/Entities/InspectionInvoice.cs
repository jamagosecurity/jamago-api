namespace Jama.Domain.Entities;

public class InspectionInvoice : BaseEntity
{
    public Guid TechnicianInspectionId { get; set; }
    public TechnicianInspection TechnicianInspection { get; set; } = null!;
    public Guid DiaInspectionId { get; set; }
    public int Quarter { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }

    /// <summary>Opaque random token granting anonymous, time-limited access to this invoice's PDF. Null when no link has been generated.</summary>
    public string? ShareToken { get; set; }
    public DateTime? ShareTokenExpiresAtUtc { get; set; }
}
