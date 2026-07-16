using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Staffs.Queries.GetActiveStaff;

public record GetActiveStaffQuery : IRequest<TypedResult<IReadOnlyList<StaffDto>>>;

public class GetActiveStaffQueryHandler : IRequestHandler<GetActiveStaffQuery, TypedResult<IReadOnlyList<StaffDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetActiveStaffQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TypedResult<IReadOnlyList<StaffDto>>> Handle(
        GetActiveStaffQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _context.Staff
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.FullName)
            .ToListAsync(cancellationToken);

        IReadOnlyList<StaffDto> result = items.Select(StaffMappings.ToDto).ToList();
        return TypedResult<IReadOnlyList<StaffDto>>.Success(result);
    }
}
