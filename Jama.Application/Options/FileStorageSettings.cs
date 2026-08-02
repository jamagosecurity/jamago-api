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
    /// Extensions an admin may upload. An allow-list rather than a block-list:
    /// anything not named here is refused, so a new dangerous type cannot slip
    /// through by being absent from a list of bad ones.
    /// </summary>
    public string[] AllowedExtensions { get; set; } =
    [
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".csv",
        ".png", ".jpg", ".jpeg", ".webp", ".heic", ".zip",
    ];
}
