namespace Jama.Domain.Entities;

public class NetworkDetail : BaseEntity
{
    public Guid TechnicianInspectionId { get; set; }
    public TechnicianInspection TechnicianInspection { get; set; } = null!;
    public string? SwitchBrand { get; set; }
    public string? SwitchModel { get; set; }
    public string? RouterBrand { get; set; }
    public string? RouterModel { get; set; }
    public string? Firewall { get; set; }
    public string? RackDetails { get; set; }
    public string? NetworkRemarks { get; set; }
}
