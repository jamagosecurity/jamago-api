namespace Jama.Domain.Entities;

public class VipClientDocument : BaseEntity
{
    public Guid VipClientFolderId { get; set; }
    public VipClientFolder Folder { get; set; } = null!;

    /// <summary>Original name as uploaded, shown in the UI and used on download.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Key handed to IFileStorage. Generated server-side from ids and a GUID, so
    /// the client's own file name never reaches the filesystem and cannot be
    /// used to escape the storage root.
    /// </summary>
    public string StorageKey { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Original size as uploaded — what the client downloads back,
    /// regardless of how the bytes sit on disk.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Whether <see cref="StorageKey"/> holds gzip-compressed bytes
    /// rather than the original file. Decided per upload — see
    /// UploadVipDocumentCommandHandler — since an already-compressed format
    /// (JPEG, PDF, ZIP, DOCX...) would only grow from a second pass.</summary>
    public bool IsCompressed { get; set; }

    public Guid UploadedById { get; set; }
}
