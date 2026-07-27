namespace Jama.Domain.Entities;

public class KpoiDetail : BaseEntity
{
    public Guid TechnicianInspectionId { get; set; }
    public TechnicianInspection TechnicianInspection { get; set; } = null!;
    public string? IvdIvss { get; set; }
    public string? KpoiCamera { get; set; }
    public string? Lens { get; set; }
    public string? HardDisc { get; set; }
}
