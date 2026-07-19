using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Staffs.Commands.DeleteStaff;

public record DeleteStaffCommand : IRequest<TypedResult<string>>
{
    public Guid Id { get; init; }
}

public class DeleteStaffCommandHandler : IRequestHandler<DeleteStaffCommand, TypedResult<string>>
{
    private readonly IApplicationDbContext _context;

    public DeleteStaffCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TypedResult<string>> Handle(DeleteStaffCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Staff
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return TypedResult<string>.Failure("Staff member not found.");
        }

        var id = entity.Id.ToString();
        _context.Staff.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return TypedResult<string>.Success(id);
    }
}
