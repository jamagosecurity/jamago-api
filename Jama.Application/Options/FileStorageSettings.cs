namespace Jama.Application.Options;

public class FileStorageSettings
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Directory uploads are written to. Kept outside the web root so nothing is
    /// served directly. On the VPS this sits beside the app under
    /// /var/www/jamago-api/storage — remember to include it in backups, since
    /// unlike the invoices it holds the only copy of a client's documents.
    /// </summary>
    public string Root { get; set; } = "storage";

    /// <summary>Largest single upload accepted, in megabytes.</summary>
    public int MaxFileSizeMb { get; set; } = 25;

    /// <summary>
    /// Largest single DRAWING file, in megabytes — kept apart from
    /// <see cref="MaxFileSizeMb"/> because a DWG with attached xrefs runs far
    /// past what a VIP document or a stock photo ever needs, and raising the
    /// shared limit to fit one would have quietly widened every other upload
    /// in the app along with it.
    /// </summary>
    public int DrawingMaxFileSizeMb { get; set; } = 150;

    /// <summary>
    /// Extensions an admin may upload. An allow-list rather than a block-list:
    /// anything not named here is refused, so a new dangerous type cannot slip
    /// through by being absent from a list of bad ones.
    /// </summary>
    public string[] AllowedExtensions { get; set; } =
    [
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".csv",
        ".png", ".jpg", ".jpeg", ".webp", ".heic", ".zip",
    ];

    /// <summary>
    /// Extensions accepted on a DRAWING specifically. Separate list rather than
    /// folded into <see cref="AllowedExtensions"/>: a DWG or DXF has no business
    /// being accepted on a VIP client folder or a camera photo, and a shared
    /// list would have let one in everywhere the other was checked.
    /// </summary>
    public string[] DrawingAllowedExtensions { get; set; } =
    [
        ".dwg", ".dxf", ".pdf", ".zip",
    ];
}
