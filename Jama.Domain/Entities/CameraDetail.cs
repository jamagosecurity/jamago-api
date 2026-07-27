namespace Jama.Domain.Entities;

public class CameraDetail : BaseEntity
{
    public Guid TechnicianInspectionId { get; set; }
    public TechnicianInspection TechnicianInspection { get; set; } = null!;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Location { get; set; }
    public string? Remarks { get; set; }
}
