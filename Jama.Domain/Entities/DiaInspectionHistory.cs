using Jama.Domain.Enums;

namespace Jama.Domain.Entities;

public class DiaInspectionHistory : BaseEntity
{
    public Guid DiaInspectionId { get; set; }
    public DiaInspection DiaInspection { get; set; } = null!;
    public DiaInspectionAction Action { get; set; }
    public Guid ActorId { get; set; }
    public string? ActorName { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
}
