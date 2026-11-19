namespace Jama.Domain.Entities;

/// <summary>One service on the MOI price list: what the technician can tick on an MOI job, and its price.
/// Separate from the Non-MOI list and from the MOI stock inventory.</summary>
public class MoiServiceItem : BaseEntity
{
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }

    public decimal Price { get; set; }
    public int SortOrder { get; set; }

    public ICollection<MoiServiceImage> Images { get; set; } = [];
}

/// <summary>A picture of an MOI service, stored like every other upload.</summary>
public class MoiServiceImage : BaseEntity
{
    public Guid CatalogueItemId { get; set; }
    public MoiServiceItem CatalogueItem { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int SortOrder { get; set; }
}
