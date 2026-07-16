using Jama.Domain.Entities;

namespace Jama.Application.Staffs;

internal static class StaffMappings
{
    internal static StaffDto ToDto(Staff entity) =>
        new(
            entity.Id,
            entity.FullName,
            entity.Role,
            entity.Responsibility,
            entity.Department,
            entity.DisplayOrder,
            entity.IsActive,
            entity.CreatedAt);
}
