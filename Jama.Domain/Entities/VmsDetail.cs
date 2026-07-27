namespace Jama.Domain.Entities;

public class VmsDetail : BaseEntity
{
    public Guid TechnicianInspectionId { get; set; }
    public TechnicianInspection TechnicianInspection { get; set; } = null!;
    public string? VmsName { get; set; }
    public string? Version { get; set; }
    public string? LicenseDetails { get; set; }
    public string? ServerDetails { get; set; }
    public string? HealthStatus { get; set; }
    public string? Remarks { get; set; }
}
