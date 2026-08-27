namespace Jama.Domain.Entities;

/// <summary>
/// One picture of a stock item. The bytes live in <c>IFileStorage</c> under
/// <see cref="StorageKey"/>; this row is only the metadata needed to list,
/// serve and order them.
/// </summary>
public class CameraImage : BaseEntity
{
    public Guid CameraId { get; set; }
    public Camera Camera { get; set; } = null!;

    /// <summary>The name as uploaded, shown to the user and used for download.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Opaque key owned by this feature, passed to IFileStorage.</summary>
    public string StorageKey { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    /// <summary>Position in the gallery; the lowest is the item's main picture.</summary>
    public int SortOrder { get; set; }
}
