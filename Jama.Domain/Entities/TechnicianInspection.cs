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
    public ICollection<CameraDetail> Cameras { get; set; } = [];
    public ICollection<InspectionInvoice> Invoices { get; set; } = [];
    public ICollection<TechnicianInspectionHistory> History { get; set; } = [];
}

public enum TechnicianInspectionStatus
{
    Draft,
    Submitted,
}

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

public class UpsGeneralDetail : BaseEntity
{
    public Guid TechnicianInspectionId { get; set; }
    public TechnicianInspection TechnicianInspection { get; set; } = null!;
    public string? UpsBrand { get; set; }
    public string? UpsCapacity { get; set; }
    public string? BatteryStatus { get; set; }
    public bool GeneratorAvailable { get; set; }
    public string? GeneratorDetails { get; set; }
    public string? GeneralRemarks { get; set; }
}

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

public class InspectionInvoice : BaseEntity
{
    public Guid TechnicianInspectionId { get; set; }
    public TechnicianInspection TechnicianInspection { get; set; } = null!;
    public Guid DiaInspectionId { get; set; }
    public int Quarter { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}

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

public enum TechnicianInspectionAction
{
    Start,
    SaveDraft,
    Submit,
    Reopen,
}
