using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Staffs.Commands.SetStaffPermissions;

/// <summary>
/// Replaces a staff account's granted permissions with exactly the supplied set.
/// Admin-only: this is how an administrator hands out access.
/// </summary>
public record SetStaffPermissionsCommand : IRequest<TypedResult<string>>
{
    /// <summary>Staff record id (not the login account id).</summary>
    public Guid Id { get; init; }
    public IReadOnlyList<string> Permissions { get; init; } = [];
}

public class SetStaffPermissionsCommandHandler
    : IRequestHandler<SetStaffPermissionsCommand, TypedResult<string>>
{
    private readonly IApplicationDbContext _context;

    public SetStaffPermissionsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TypedResult<string>> Handle(
        SetStaffPermissionsCommand request,
        CancellationToken cancellationToken)
    {
        var staff = await _context.Staff
            .Include(s => s.Account)
            .ThenInclude(a => a!.Permissions)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (staff is null)
        {
            return TypedResult<string>.Failure("Staff member not found.");
        }

        if (staff.Account is null)
        {
            return TypedResult<string>.Failure("This staff member has no login account.");
        }

        // Admins already hold every permission implicitly; storing grants for them
        // would be misleading and could not remove access anyway.
        if (staff.Account.Role == Roles.Admin)
        {
            return TypedResult<string>.Failure("Administrators already have full access.");
        }

        var requested = request.Permissions
            .Where(Permissions.IsValid)
            .Distinct()
            .ToHashSet();

        var existing = staff.Account.Permissions.ToList();

        var toRemove = existing.Where(p => !requested.Contains(p.Permission)).ToList();
        foreach (var permission in toRemove)
        {
            _context.UserPermissions.Remove(permission);
        }

        var currentKeys = existing.Select(p => p.Permission).ToHashSet();
        foreach (var key in requested.Where(k => !currentKeys.Contains(k)))
        {
            _context.UserPermissions.Add(new UserPermission
            {
                Id = Guid.CreateVersion7(),
                AdminUserId = staff.Account.Id,
                Permission = key,
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        return TypedResult<string>.Success(staff.Id.ToString());
    }
}
