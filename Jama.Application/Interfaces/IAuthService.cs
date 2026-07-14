using Jama.Application.DTOs;
using Jama.Domain.Entities;

namespace Jama.Application.Interfaces;

public interface IAdminUserRepository
{
    Task<AdminUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AdminUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
    Task AddAsync(AdminUser user, CancellationToken ct = default);
}

public interface IPasswordHasher
{
    string Hash(AdminUser user, string password);
    bool Verify(AdminUser user, string password);
}

public interface ITokenGenerator
{
    LoginResponse Generate(AdminUser user);
}

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<UserSummaryDto?> GetUserAsync(Guid userId, CancellationToken ct = default);
}
