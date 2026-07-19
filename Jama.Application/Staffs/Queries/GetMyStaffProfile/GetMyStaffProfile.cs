using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Staffs.Queries.GetMyStaffProfile;

public record GetMyStaffProfileQuery : IRequest<TypedResult<AdminStaffDto>>
{
    public Guid UserId { get; init; }
}

public class GetMyStaffProfileQueryHandler
    : IRequestHandler<GetMyStaffProfileQuery, TypedResult<AdminStaffDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMyStaffProfileQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TypedResult<AdminStaffDto>> Handle(
        GetMyStaffProfileQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await _context.Staff
            .AsNoTracking()
            .Include(s => s.Account)
            .FirstOrDefaultAsync(s => s.AdminUserId == request.UserId, cancellationToken);

        if (entity is null)
        {
            return TypedResult<AdminStaffDto>.Failure("Staff profile not found.");
        }

        return TypedResult<AdminStaffDto>.Success(StaffMappings.ToAdminDto(entity));
    }
}
