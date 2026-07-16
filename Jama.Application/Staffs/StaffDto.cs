namespace Jama.Application.Staffs;

public record StaffDto(
    Guid Id,
    string FullName,
    string Role,
    string Responsibility,
    string? Department,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt);
