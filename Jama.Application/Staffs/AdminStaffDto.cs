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
    bool IsActive,
    DateTime CreatedAt);
