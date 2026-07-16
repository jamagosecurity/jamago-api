using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Staffs.Commands.UpdateStaff;

public record UpdateStaffCommand : IRequest<TypedResult<Guid>>
{
    public Guid Id { get; init; }
    public string? FullName { get; init; }
    public string? Role { get; init; }
    public string? Responsibility { get; init; }
    public string? Department { get; init; }
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; }
}

public class UpdateStaffCommandHandler : IRequestHandler<UpdateStaffCommand, TypedResult<Guid>>
{
    private readonly IApplicationDbContext _context;

    public UpdateStaffCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TypedResult<Guid>> Handle(UpdateStaffCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Staff
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return TypedResult<Guid>.Failure("Staff member not found.");
        }

        entity.FullName = request.FullName!;
        entity.Role = request.Role!;
        entity.Responsibility = request.Responsibility!;
        entity.Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim();
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return TypedResult<Guid>.Success(entity.Id);
    }
}
