using Jama.Application.Auth;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using Jama.Application.Staffs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Staffs.Commands.UpdateStaff;

public record UpdateStaffCommand : IRequest<TypedResult<string>>
{
    public Guid Id { get; init; }
    public string? FullName { get; init; }
    public string? Email { get; init; }
    public string? Password { get; init; }
    public StaffDepartment? Department { get; init; }
    public bool IsActive { get; init; }
}

public class UpdateStaffCommandHandler : IRequestHandler<UpdateStaffCommand, TypedResult<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public UpdateStaffCommandHandler(IApplicationDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<TypedResult<string>> Handle(UpdateStaffCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Staff
            .Include(s => s.Account)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return TypedResult<string>.Failure("Staff member not found.");
        }

        var fullName = request.FullName!.Trim();
        var email = request.Email!.Trim().ToLowerInvariant();

        if (entity.Account is null)
        {
            var account = new AdminUser
            {
                Id = Guid.CreateVersion7(),
                Email = email,
                FullName = fullName,
                Role = Roles.Staff,
                IsActive = request.IsActive,
            };
            account.PasswordHash = _passwordHasher.Hash(account, request.Password!);
            entity.AdminUserId = account.Id;
            entity.Account = account;
            _context.AdminUsers.Add(account);
        }
        else
        {
            entity.Account.Email = email;
            entity.Account.FullName = fullName;
            entity.Account.IsActive = request.IsActive;
            entity.Account.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                entity.Account.PasswordHash = _passwordHasher.Hash(entity.Account, request.Password);
            }
        }

        entity.FullName = fullName;
        entity.Department = request.Department?.ToDisplayName();
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return TypedResult<string>.Success(entity.Id.ToString());
    }
}
