namespace Jama.Domain.Entities;

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
