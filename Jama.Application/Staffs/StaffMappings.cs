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

    internal static AdminStaffDto ToAdminDto(Staff entity) =>
        new(
            entity.Id,
            entity.FullName,
            entity.Account?.Email,
            entity.Account is not null,
            entity.Role,
            entity.Responsibility,
            entity.Department,
            entity.DisplayOrder,
            entity.IsActive,
            // No account at all means nothing to sign in with.
            entity.Account?.IsActive ?? false,
            entity.CreatedAt,
            // Admins hold everything implicitly, so report the full set for them
            // rather than the (empty) stored grants.
            entity.Account is null
                ? []
                : entity.Account.Role == Common.Roles.Admin
                    ? Common.Permissions.ForRole(Common.Roles.Admin)
                    : entity.Account.Permissions.Select(p => p.Permission).OrderBy(p => p).ToList());
}
