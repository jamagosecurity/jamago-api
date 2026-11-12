namespace Jama.Domain.Entities;

public class AdminUser : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "Admin";
    public bool IsActive { get; set; } = true;
    public Staff? StaffProfile { get; set; }

    /// <summary>
    /// When this account last opened its notifications.
    ///
    /// One marker rather than a read flag per notification: the notifications
    /// are not rows of their own, they are the approval trail read backwards, so
    /// there is nothing to flag. Anything decided after this moment is unread.
    ///
    /// Null means never opened, which correctly makes everything unread rather
    /// than nothing.
    /// </summary>
    public DateTime? NotificationsSeenAt { get; set; }

    // ===== Profile: who this person is, in their own words =====

    /// <summary>The person's own job title ("Senior Technician"), written by them. Shown beside their name.</summary>
    public string? Designation { get; set; }

    /// <summary>The number colleagues can reach them on, written by them (or an administrator).</summary>
    public string? Phone { get; set; }

    /// <summary>Where their profile picture is stored (see IFileStorage). Null when they have none.</summary>
    public string? PhotoKey { get; set; }
    public string? PhotoContentType { get; set; }

    /// <summary>When the picture last changed. Doubles as its version, so a new picture is fetched, not served from cache.</summary>
    public DateTime? PhotoUpdatedAt { get; set; }

    // ===== Forgotten password =====

    /// <summary>SHA-256 of the one-time reset code emailed to the person. Never the code itself.</summary>
    public string? PasswordResetTokenHash { get; set; }
    public DateTime? PasswordResetExpiresAt { get; set; }
    public ICollection<UserPermission> Permissions { get; set; } = [];
}
