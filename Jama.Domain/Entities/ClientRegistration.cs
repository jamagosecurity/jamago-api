using Jama.Domain.Enums;

namespace Jama.Domain.Entities;

/// <summary>
/// A client who signed up on the website, waiting on — or past — an admin's
/// decision.
///
/// The login account is created at sign-up but switched off
/// (<see cref="AdminUser.IsActive"/> false) until an admin accepts it, so the
/// password the client chose is the one they keep; nothing has to be sent to
/// them afterwards. Kept as its own row rather than columns on the account:
/// VIP clients share the Client role without ever having registered, and the
/// review trail (who decided, when, why) belongs to the registration.
/// </summary>
public class ClientRegistration : BaseEntity
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>Lowercased, same as the account's — the account's unique index
    /// is the real guard against duplicates.</summary>
    public string Email { get; set; } = string.Empty;

    public string Mobile { get; set; } = string.Empty;

    /// <summary>Where the client's site is. Becomes the DIA inspection's
    /// client location when the client is accepted as MOI.</summary>
    public string SiteLocation { get; set; } = string.Empty;

    public ClientRegistrationStatus Status { get; set; } = ClientRegistrationStatus.Pending;

    /// <summary>Null until accepted.</summary>
    public ClientCategory? Category { get; set; }

    /// <summary>
    /// The staff departments allotted to this client, by their stable enum
    /// name (StaffDepartment in the application layer) — never the display
    /// label, so renaming a department's label cannot orphan an allotment.
    /// Empty until accepted.
    /// </summary>
    public List<string> Teams { get; set; } = [];

    public Guid AdminUserId { get; set; }
    public AdminUser Account { get; set; } = null!;

    /// <summary>The DIA inspection opened when this client was accepted as
    /// MOI. Null for Non-MOI, and set back to null if that DIA record is later
    /// deleted — the registration outlives it.</summary>
    public Guid? DiaInspectionId { get; set; }
    public DiaInspection? DiaInspection { get; set; }

    public Guid? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Why it was turned down. Internal: the admin list shows it so a
    /// second reviewer knows what was already decided, but the client is only
    /// ever told it was not approved — admins write these for each other.</summary>
    public string? RejectionReason { get; set; }
}
