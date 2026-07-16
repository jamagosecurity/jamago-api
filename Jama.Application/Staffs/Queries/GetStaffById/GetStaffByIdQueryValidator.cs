using FluentValidation;
using Jama.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Staffs.Queries.GetStaffById;

public class GetStaffByIdQueryValidator : AbstractValidator<GetStaffByIdQuery>
{
    private readonly IApplicationDbContext _context;

    public GetStaffByIdQueryValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(v => v.Id)
            .NotEmpty().WithMessage("Staff Id is required.")
            .MustAsync(StaffExists).WithMessage("Staff member not found.");
    }

    private Task<bool> StaffExists(Guid id, CancellationToken token) =>
        _context.Staff.AnyAsync(s => s.Id == id, cancellationToken: token);
}
