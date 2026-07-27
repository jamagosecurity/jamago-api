using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Application.Auth;
using Jama.Application.Common;
using Jama.Domain.Entities;
using Jama.Application.Staffs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Staffs.Commands.CreateStaff;

public record CreateStaffCommand : IRequest<TypedResult<string>>
{
    public string? FullName { get; init; }
    public string? Email { get; init; }
    public string? Password { get; init; }
    public StaffDepartment? Department { get; init; }
    /// <summary>Show this person in the public "Our Team" section.</summary>
    public bool IsActive { get; init; } = true;
    /// <summary>
    /// Whether the login account is enabled — independent of public visibility.
    /// See <see cref="UpdateStaff.UpdateStaffCommand.CanSignIn"/>.
    /// </summary>
    public bool CanSignIn { get; init; } = true;

    /// <summary>Explicit grants. When omitted, department defaults are applied.</summary>
    public IReadOnlyList<string>? Permissions { get; init; }
}

public class CreateStaffCommandHandler : IRequestHandler<CreateStaffCommand, TypedResult<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public CreateStaffCommandHandler(IApplicationDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<TypedResult<string>> Handle(CreateStaffCommand request, CancellationToken cancellationToken)
    {
        var fullName = request.FullName!.Trim();
        var email = request.Email!.Trim().ToLowerInvariant();
        var account = new AdminUser
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            FullName = fullName,
            Role = request.Department.ToAuthRole(),
            IsActive = request.CanSignIn,
        };
        account.PasswordHash = _passwordHasher.Hash(account, request.Password!);

        var nextDisplayOrder = (await _context.Staff
            .MaxAsync(s => (int?)s.DisplayOrder, cancellationToken) ?? 0) + 1;

        var entity = new Staff
        {
            Id = Guid.CreateVersion7(),
            AdminUserId = account.Id,
            Account = account,
            FullName = fullName,
            Role = "Staff Member",
            Responsibility = string.Empty,
            Department = request.Department?.ToDisplayName(),
            DisplayOrder = nextDisplayOrder,
            IsActive = request.IsActive,
        };

        _context.AdminUsers.Add(account);
        _context.Staff.Add(entity);

        // Seed sensible starting access for the chosen department. The admin can
        // adjust the exact grants afterwards from the staff editor.
        var defaults = request.Permissions is { Count: > 0 }
            ? request.Permissions.Where(Permissions.IsValid).Distinct()
            : Permissions.DefaultsForDepartment(entity.Department);

        foreach (var permission in defaults)
        {
            _context.UserPermissions.Add(new UserPermission
            {
                Id = Guid.CreateVersion7(),
                AdminUserId = account.Id,
                Permission = permission,
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        return TypedResult<string>.Success(entity.Id.ToString());
    }
}
