using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Staffs.Commands.UpdateMyStaffProfile;

/// <summary>
/// Lets a staff member edit their own profile. Deliberately narrower than
/// <c>UpdateStaffCommand</c>: email, role, department, active state and password
/// are all omitted so self-service editing can never be used to escalate
/// privileges or re-enable a disabled account. <see cref="UserId"/> comes from
/// the caller's token, never the request body.
/// </summary>
public record UpdateMyStaffProfileCommand : IRequest<TypedResult<string>>
{
    public Guid UserId { get; init; }
    public string? FullName { get; init; }
    public string? Responsibility { get; init; }
}

public class UpdateMyStaffProfileCommandHandler
    : IRequestHandler<UpdateMyStaffProfileCommand, TypedResult<string>>
{
    private readonly IApplicationDbContext _context;

    public UpdateMyStaffProfileCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TypedResult<string>> Handle(
        UpdateMyStaffProfileCommand request,
        CancellationToken cancellationToken)
    {
        var staff = await _context.Staff
            .Include(s => s.Account)
            .FirstOrDefaultAsync(s => s.AdminUserId == request.UserId, cancellationToken);

        if (staff is null)
        {
            return TypedResult<string>.Failure("Staff profile not found.");
        }

        staff.FullName = request.FullName!.Trim();
        staff.Responsibility = request.Responsibility?.Trim() ?? string.Empty;
        staff.UpdatedAt = DateTime.UtcNow;

        // Keep the display name on the login account in step, since that is what
        // the portal header and admin listings read.
        if (staff.Account is not null)
        {
            staff.Account.FullName = staff.FullName;
            staff.Account.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return TypedResult<string>.Success(staff.Id.ToString());
    }
}
