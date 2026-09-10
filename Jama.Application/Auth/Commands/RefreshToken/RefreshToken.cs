using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Auth.Commands.RefreshToken;

/// <summary>
/// Reissues the caller's token with a fresh expiry, provided their current one
/// still works — this endpoint requires authorization, so an already-expired
/// token cannot use it to reach back in. That is what keeps this a sliding
/// window rather than an indefinite one: the caller must check in again
/// before the clock runs out, not after.
///
/// The user is re-read from the database rather than trusting the old
/// token's claims, for the same reason <see cref="Login.LoginCommandHandler"/>
/// reads them fresh — an account disabled or stripped of a permission mid-day
/// must not have that outlive the moment its owner still happens to be
/// clicking around.
/// </summary>
public record RefreshTokenCommand : IRequest<TypedResult<LoginResponse>>
{
    public Guid UserId { get; init; }
}

public class RefreshTokenCommandHandler(
    IApplicationDbContext context,
    ITokenGenerator tokenGenerator)
    : IRequestHandler<RefreshTokenCommand, TypedResult<LoginResponse>>
{
    public async Task<TypedResult<LoginResponse>> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        var user = await context.AdminUsers
            .Include(u => u.Permissions)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user is null || !user.IsActive)
            return TypedResult<LoginResponse>.Failure("Session no longer valid.");

        return TypedResult<LoginResponse>.Success(tokenGenerator.Generate(user));
    }
}
