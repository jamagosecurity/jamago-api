namespace Jama.Domain.Entities;

public class DiaInspection : BaseEntity
{
    public string DiaNumber { get; set; } = string.Empty;
    public string NormalizedDiaNumber { get; set; } = string.Empty;
    public string ClientNumber { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientLocation { get; set; } = string.Empty;
    /// <summary>
    /// WGS 84 site pin, so a technician can navigate straight to the site instead of
    /// reading the address off the card. Null until an admin pins it, and always set
    /// or cleared as a pair — half a pin is not a place.
    /// </summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime? ActivatedDate { get; set; }
    public DateTime? InspectionStartedDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsArchived { get; set; }
    public Guid CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }
    public ICollection<DiaInspectionHistory> History { get; set; } = [];
}
