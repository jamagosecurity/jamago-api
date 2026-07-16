using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using MediatR;

namespace Jama.Application.Staffs.Commands.CreateStaff;

public record CreateStaffCommand : IRequest<TypedResult<Guid>>
{
    public string? FullName { get; init; }
    public string? Role { get; init; }
    public string? Responsibility { get; init; }
    public string? Department { get; init; }
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; } = true;
}

public class CreateStaffCommandHandler : IRequestHandler<CreateStaffCommand, TypedResult<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateStaffCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TypedResult<Guid>> Handle(CreateStaffCommand request, CancellationToken cancellationToken)
    {
        var entity = new Staff
        {
            Id = Guid.CreateVersion7(),
            FullName = request.FullName!,
            Role = request.Role!,
            Responsibility = request.Responsibility!,
            Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim(),
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
        };

        _context.Staff.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return TypedResult<Guid>.Success(entity.Id);
    }
}
