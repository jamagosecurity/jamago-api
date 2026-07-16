using Jama.Domain.Entities;

namespace Jama.Application.Auth;

public interface IPasswordHasher
{
    string Hash(AdminUser user, string password);
    bool Verify(AdminUser user, string password);
}

public interface ITokenGenerator
{
    LoginResponse Generate(AdminUser user);
}
