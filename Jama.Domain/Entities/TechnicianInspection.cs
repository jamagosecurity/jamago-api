using Jama.Domain.Enums;

namespace Jama.Domain.Entities;

public class TechnicianInspection : BaseEntity
{
    public Guid DiaInspectionId { get; set; }
    public DiaInspection DiaInspection { get; set; } = null!;
    public int Quarter { get; set; }
    public Guid TechnicianId { get; set; }
    public TechnicianInspectionStatus Status { get; set; } = TechnicianInspectionStatus.Draft;
    public DateTime? SubmittedAt { get; set; }
    public bool IsDeleted { get; set; }

    public NetworkDetail? Network { get; set; }
    public VmsDetail? Vms { get; set; }
    public UpsGeneralDetail? UpsGeneral { get; set; }
    public AnprConfiguration? Anpr { get; set; }
    public KpoiDetail? Kpoi { get; set; }
    public ICollection<CameraDetail> Cameras { get; set; } = [];
    public ICollection<InspectionInvoice> Invoices { get; set; } = [];
    public ICollection<TechnicianInspectionHistory> History { get; set; } = [];
}
