using FluentValidation;
using Jama.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Staffs.Commands.UpdateStaff;

public class UpdateStaffCommandValidator : AbstractValidator<UpdateStaffCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateStaffCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(v => v.Id)
            .NotEmpty().WithMessage("Staff Id is required.")
            .MustAsync(StaffExists).WithMessage("Staff member not found.");

        RuleFor(v => v.FullName)
            .NotEmpty().WithMessage("Full Name is required.")
            .MaximumLength(150).WithMessage("Full Name must not exceed 150 characters.")
            .MustAsync(BeUniqueStaffMember).WithMessage("Staff member with this name and role already exists!");

        RuleFor(v => v.Role)
            .NotEmpty().WithMessage("Role is required.")
            .MaximumLength(120).WithMessage("Role must not exceed 120 characters.");

        RuleFor(v => v.Responsibility)
            .NotEmpty().WithMessage("Responsibility is required.")
            .MaximumLength(1000).WithMessage("Responsibility must not exceed 1000 characters.");

        RuleFor(v => v.Department)
            .MaximumLength(120).WithMessage("Department must not exceed 120 characters.");

        RuleFor(v => v.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Display Order must be zero or greater.");
    }

    private Task<bool> StaffExists(Guid id, CancellationToken token) =>
        _context.Staff.AnyAsync(s => s.Id == id, cancellationToken: token);

    private async Task<bool> BeUniqueStaffMember(
        UpdateStaffCommand model,
        string? fullName,
        CancellationToken token)
    {
        return !await _context.Staff
            .AnyAsync(
                s => s.Id != model.Id && s.FullName == fullName && s.Role == model.Role,
                cancellationToken: token);
    }
}
