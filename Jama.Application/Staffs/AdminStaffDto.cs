namespace Jama.Application.Staffs;

public record AdminStaffDto(
    Guid Id,
    string FullName,
    string? Email,
    bool HasLoginAccount,
    string Role,
    string Responsibility,
    string? Department,
    int DisplayOrder,
    /// <summary>Visible in the public "Our Team" section.</summary>
    bool IsActive,
    /// <summary>Login account enabled. Independent of <paramref name="IsActive"/>.</summary>
    bool CanSignIn,
    DateTime CreatedAt,
    IReadOnlyList<string> Permissions,
    /// <summary>The login account behind this member, which the profile endpoints are addressed by.</summary>
    Guid? AccountId = null,
    string? Designation = null,
    long PhotoVersion = 0,
    string? Phone = null);
