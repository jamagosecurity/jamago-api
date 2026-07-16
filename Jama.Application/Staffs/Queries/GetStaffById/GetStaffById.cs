using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Staffs.Queries.GetStaffById;

public record GetStaffByIdQuery : IRequest<TypedResult<StaffDto>>
{
    public Guid Id { get; init; }
}

public class GetStaffByIdQueryHandler : IRequestHandler<GetStaffByIdQuery, TypedResult<StaffDto>>
{
    private readonly IApplicationDbContext _context;

    public GetStaffByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TypedResult<StaffDto>> Handle(GetStaffByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.Staff
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return TypedResult<StaffDto>.Failure("Staff member not found.");
        }

        return TypedResult<StaffDto>.Success(StaffMappings.ToDto(entity));
    }
}
