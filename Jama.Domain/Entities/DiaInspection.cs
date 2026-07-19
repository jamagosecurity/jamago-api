namespace Jama.Domain.Entities;

public class DiaInspection : BaseEntity
{
    public string DiaNumber { get; set; } = string.Empty;
    public string NormalizedDiaNumber { get; set; } = string.Empty;
    public string ClientNumber { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientLocation { get; set; } = string.Empty;
    public DateTime? ActivatedDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsArchived { get; set; }
    public Guid CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }
    public ICollection<DiaInspectionHistory> History { get; set; } = [];
}

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

public enum DiaInspectionAction
{
    Create,
    Update,
    Activate,
    Deactivate,
    Archive,
}
