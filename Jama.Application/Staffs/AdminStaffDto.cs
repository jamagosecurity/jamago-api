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
    IReadOnlyList<string> Permissions);
