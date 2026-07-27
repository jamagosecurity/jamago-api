using Jama.Domain.Enums;

namespace Jama.Domain.Entities;

public class TechnicianInspectionHistory : BaseEntity
{
    public Guid TechnicianInspectionId { get; set; }
    public TechnicianInspection TechnicianInspection { get; set; } = null!;
    public Guid? DiaInspectionId { get; set; }
    public TechnicianInspectionAction Action { get; set; }
    public Guid ActorId { get; set; }
    public string? ActorName { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
}
