using FluentValidation;
using Jama.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Staffs.Commands.DeleteStaff;

public class DeleteStaffCommandValidator : AbstractValidator<DeleteStaffCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteStaffCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(v => v.Id)
            .NotEmpty().WithMessage("Staff Id is required.")
            .MustAsync(StaffExists).WithMessage("Staff member not found.");
    }

    private Task<bool> StaffExists(Guid id, CancellationToken token) =>
        _context.Staff.AnyAsync(s => s.Id == id, cancellationToken: token);
}
