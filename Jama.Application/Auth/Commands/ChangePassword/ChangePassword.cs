using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Auth.Commands.ChangePassword;

/// <summary>
/// Self-service password change. <see cref="UserId"/> is taken from the caller's
/// token rather than the request body so a user can only ever change their own
/// password.
/// </summary>
public record ChangePasswordCommand : IRequest<TypedResult<string>>
{
    public Guid UserId { get; init; }
    public string? CurrentPassword { get; init; }
    public string? NewPassword { get; init; }
}

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, TypedResult<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public ChangePasswordCommandHandler(IApplicationDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<TypedResult<string>> Handle(
        ChangePasswordCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _context.AdminUsers
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return TypedResult<string>.Failure("Account not found.");
        }

        // Re-check the current password so a hijacked session cannot lock the
        // real owner out by silently changing their credentials.
        if (!_passwordHasher.Verify(user, request.CurrentPassword!))
        {
            return TypedResult<string>.Failure("Your current password is incorrect.");
        }

        user.PasswordHash = _passwordHasher.Hash(user, request.NewPassword!);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return TypedResult<string>.Success("Password updated.");
    }
}
