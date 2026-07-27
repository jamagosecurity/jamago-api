namespace Jama.Domain.Entities;

public class AnprConfiguration : BaseEntity
{
    public Guid TechnicianInspectionId { get; set; }
    public TechnicianInspection TechnicianInspection { get; set; } = null!;
    public bool AnprInstalled { get; set; }
    public string? CameraDetails { get; set; }
    public string? Configuration { get; set; }
    public string? SoftwareVersion { get; set; }
    public string? Remarks { get; set; }
}
