namespace Jama.Domain.Entities;

/// <summary>
/// One uploaded file on a drawing — the native DWG/DXF, a plotted PDF, or a
/// ZIP of the drawing with its xrefs. A drawing typically carries at least two:
/// the CAD file to edit and the PDF an approver can actually look at, since
/// neither DWG nor DXF renders in a browser.
/// </summary>
public class DrawingFile : BaseEntity
{
    public Guid DrawingId { get; set; }
    public Drawing Drawing { get; set; } = null!;

    /// <summary>Original name as uploaded, shown in the UI and used on download.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Key handed to IFileStorage. Generated server-side from ids and a GUID, so
    /// the client's own file name never reaches the filesystem and cannot be
    /// used to escape the storage root.
    /// </summary>
    public string StorageKey { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }

    public Guid UploadedById { get; set; }

    /// <summary>Copied at upload time, on the same terms as a quotation line's
    /// price: the uploader's account may later be renamed or removed, and a
    /// file list that rewrites itself when it is would be no record at all.</summary>
    public string? UploadedByName { get; set; }
}
