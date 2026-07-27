namespace Jama.Domain.Entities;

public class DiaInspection : BaseEntity
{
    public string DiaNumber { get; set; } = string.Empty;
    public string NormalizedDiaNumber { get; set; } = string.Empty;
    public string ClientNumber { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientLocation { get; set; } = string.Empty;
    public DateTime? ActivatedDate { get; set; }
    public DateTime? InspectionStartedDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsArchived { get; set; }
    public Guid CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }
    public ICollection<DiaInspectionHistory> History { get; set; } = [];
}
