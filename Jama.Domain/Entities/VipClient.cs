namespace Jama.Domain.Entities;

/// <summary>
/// A VIP client project handed over by an admin. Each one owns a login account
/// and a fixed set of document folders.
/// </summary>
public class VipClient : BaseEntity
{
    public string ClientName { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the project's main folder. Defaults to
    /// "{ClientName} - {ProjectName}" at creation but can be renamed afterwards
    /// without touching where files actually live, which is keyed by Id.
    /// </summary>
    public string FolderName { get; set; } = string.Empty;

    /// <summary>Login account for the client. Role is Roles.Client.</summary>
    public Guid AdminUserId { get; set; }
    public AdminUser Account { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    public bool IsArchived { get; set; }

    public Guid CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }

    public ICollection<VipClientFolder> Folders { get; set; } = [];
}
