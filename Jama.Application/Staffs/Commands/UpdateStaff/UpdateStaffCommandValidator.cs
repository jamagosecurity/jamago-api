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
            .MaximumLength(150).WithMessage("Full Name must not exceed 150 characters.");

        RuleFor(v => v.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Enter a valid email address.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");

        RuleFor(v => v)
            .MustAsync(BeUniqueEmail).WithMessage("An account with this email already exists.")
            .MustAsync(HavePasswordForNewAccount).WithMessage("Password is required for staff without a login account.");

        When(v => !string.IsNullOrWhiteSpace(v.Password), () =>
        {
            RuleFor(v => v.Password)
                .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
                .Matches("[A-Z]").WithMessage("Password must include an uppercase letter.")
                .Matches("[a-z]").WithMessage("Password must include a lowercase letter.")
                .Matches("[0-9]").WithMessage("Password must include a number.");
        });

        RuleFor(v => v.Department)
            .IsInEnum().WithMessage("Select a valid department.");
    }

    private Task<bool> StaffExists(Guid id, CancellationToken token) =>
        _context.Staff.AnyAsync(s => s.Id == id, cancellationToken: token);

    private async Task<bool> BeUniqueEmail(UpdateStaffCommand model, CancellationToken token)
    {
        var normalized = model.Email?.Trim().ToLowerInvariant();
        var accountId = await _context.Staff
            .Where(s => s.Id == model.Id)
            .Select(s => s.AdminUserId)
            .FirstOrDefaultAsync(token);

        return !await _context.Staff
            .Where(s => s.AdminUserId != accountId)
            .AnyAsync(s => s.Account != null && s.Account.Email == normalized, token)
            && !await _context.AdminUsers.AnyAsync(u => u.Id != accountId && u.Email == normalized, token);
    }

    private async Task<bool> HavePasswordForNewAccount(UpdateStaffCommand model, CancellationToken token) =>
        !await _context.Staff.AnyAsync(s => s.Id == model.Id && s.AdminUserId == null, token)
        || !string.IsNullOrWhiteSpace(model.Password);
}
