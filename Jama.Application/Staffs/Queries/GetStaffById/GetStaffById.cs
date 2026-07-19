using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Staffs.Queries.GetStaffById;

public record GetStaffByIdQuery : IRequest<TypedResult<AdminStaffDto>>
{
    public Guid Id { get; init; }
}

public class GetStaffByIdQueryHandler : IRequestHandler<GetStaffByIdQuery, TypedResult<AdminStaffDto>>
{
    private readonly IApplicationDbContext _context;

    public GetStaffByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TypedResult<AdminStaffDto>> Handle(GetStaffByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.Staff
            .AsNoTracking()
            .Include(s => s.Account)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return TypedResult<AdminStaffDto>.Failure("Staff member not found.");
        }

        return TypedResult<AdminStaffDto>.Success(StaffMappings.ToAdminDto(entity));
    }
}
