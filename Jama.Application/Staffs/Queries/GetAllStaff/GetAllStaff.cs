using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Staffs.Queries.GetAllStaff;

public record GetAllStaffQuery : IRequest<TypedResult<IReadOnlyList<AdminStaffDto>>>;

public class GetAllStaffQueryHandler : IRequestHandler<GetAllStaffQuery, TypedResult<IReadOnlyList<AdminStaffDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAllStaffQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TypedResult<IReadOnlyList<AdminStaffDto>>> Handle(
        GetAllStaffQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _context.Staff
            .AsNoTracking()
            .Include(s => s.Account)
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.FullName)
            .ToListAsync(cancellationToken);

        IReadOnlyList<AdminStaffDto> result = items.Select(StaffMappings.ToAdminDto).ToList();
        return TypedResult<IReadOnlyList<AdminStaffDto>>.Success(result);
    }
}
