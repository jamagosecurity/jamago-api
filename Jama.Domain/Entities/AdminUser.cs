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
    public ICollection<UserPermission> Permissions { get; set; } = [];
}
