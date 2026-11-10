using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Maintenance.Queries.GetAssignableStaff;

/// <summary>Active staff holding a login account — the assignment picker's
/// own list, queried directly rather than through /api/staff/all, which is
/// Admin-role-only and would 403 a maintenance.manage holder who is not an
/// administrator.</summary>
public sealed record GetAssignableStaffQuery : IRequest<ApiResult<IReadOnlyList<AssignableStaffDto>>>;

public sealed class GetAssignableStaffQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAssignableStaffQuery, ApiResult<IReadOnlyList<AssignableStaffDto>>>
{
    public async Task<ApiResult<IReadOnlyList<AssignableStaffDto>>> Handle(
        GetAssignableStaffQuery request, CancellationToken cancellationToken)
    {
        var staff = await context.Staff
            .AsNoTracking()
            .Where(s => s.IsActive && s.AdminUserId != null)
            .OrderBy(s => s.FullName)
            .Select(s => new AssignableStaffDto(s.AdminUserId!.Value, s.FullName, s.Department))
            .ToListAsync(cancellationToken);

        return ApiResult<IReadOnlyList<AssignableStaffDto>>.Success(staff);
    }
}
