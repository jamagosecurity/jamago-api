namespace Jama.Domain.Entities;

/// <summary>
/// A single permission granted to a login account by an administrator.
/// Modelled as rows rather than a delimited column so grants can be indexed,
/// queried and revoked individually.
/// </summary>
public class UserPermission : BaseEntity
{
    public Guid AdminUserId { get; set; }
    public AdminUser? User { get; set; }
    public string Permission { get; set; } = string.Empty;
}
