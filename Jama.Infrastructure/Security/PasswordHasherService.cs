using Jama.Application.Auth;
using Jama.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Jama.Infrastructure.Security;

public class PasswordHasherService : IPasswordHasher
{
    private readonly PasswordHasher<AdminUser> _hasher = new();

    public string Hash(AdminUser user, string password) =>
        _hasher.HashPassword(user, password);

    public bool Verify(AdminUser user, string password) =>
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}
