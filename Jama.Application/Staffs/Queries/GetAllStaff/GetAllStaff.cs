using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Staffs.Queries.GetAllStaff;

public record GetAllStaffQuery : IRequest<TypedResult<IReadOnlyList<StaffDto>>>;

public class GetAllStaffQueryHandler : IRequestHandler<GetAllStaffQuery, TypedResult<IReadOnlyList<StaffDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAllStaffQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TypedResult<IReadOnlyList<StaffDto>>> Handle(
        GetAllStaffQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _context.Staff
            .AsNoTracking()
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.FullName)
            .ToListAsync(cancellationToken);

        IReadOnlyList<StaffDto> result = items.Select(StaffMappings.ToDto).ToList();
        return TypedResult<IReadOnlyList<StaffDto>>.Success(result);
    }
}
