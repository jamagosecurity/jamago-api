using FluentValidation;
using Jama.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Staffs.Commands.CreateStaff;

public class CreateStaffCommandValidator : AbstractValidator<CreateStaffCommand>
{
    private readonly IApplicationDbContext _context;

    public CreateStaffCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(v => v.FullName)
            .NotEmpty().WithMessage("Full Name is required.")
            .MaximumLength(150).WithMessage("Full Name must not exceed 150 characters.");

        RuleFor(v => v.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Enter a valid email address.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.")
            .MustAsync(BeUniqueEmail).WithMessage("An account with this email already exists.");

        RuleFor(v => v.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must include an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must include a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must include a number.");

        RuleFor(v => v.Department)
            .IsInEnum().WithMessage("Select a valid department.");
    }

    private Task<bool> BeUniqueEmail(string? email, CancellationToken token)
    {
        var normalized = email?.Trim().ToLowerInvariant();
        return _context.AdminUsers.AllAsync(u => u.Email != normalized, token);
    }
}
