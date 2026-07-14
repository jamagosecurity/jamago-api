using Jama.Application.DTOs;
using Jama.Application.Interfaces;

namespace Jama.Application.Services;

public class AuthService(
    IAdminUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenGenerator tokenGenerator) : IAuthService
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await users.GetByEmailAsync(email, ct);

        if (user is null || !user.IsActive || !passwordHasher.Verify(user, request.Password))
        {
            return null;
        }

        return tokenGenerator.Generate(user);
    }

    public async Task<UserSummaryDto?> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive)
        {
            return null;
        }

        return new UserSummaryDto(user.Id, user.Email, user.FullName, user.Role);
    }
}
