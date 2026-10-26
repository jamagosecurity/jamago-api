using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Auth.Commands.Login;

public record LoginCommand : IRequest<TypedResult<LoginResponse>>
{
    public string? Email { get; init; }
    public string? Password { get; init; }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, TypedResult<LoginResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenGenerator _tokenGenerator;

    public LoginCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ITokenGenerator tokenGenerator)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<TypedResult<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email!.Trim().ToLowerInvariant();
        var user = await _context.AdminUsers
            .Include(u => u.Permissions)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || !_passwordHasher.Verify(user, request.Password!))
        {
            return TypedResult<LoginResponse>.Failure("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            // Only reached with the right password, so saying WHY the account
            // is off tells nobody anything they could not already find out —
            // and "invalid password" to a client who registered yesterday, typed
            // it correctly, and is simply waiting on us, reads as a broken site.
            var registrationStatus = await _context.ClientRegistrations
                .Where(r => r.AdminUserId == user.Id)
                .Select(r => (ClientRegistrationStatus?)r.Status)
                .FirstOrDefaultAsync(cancellationToken);

            return TypedResult<LoginResponse>.Failure(registrationStatus switch
            {
                ClientRegistrationStatus.Pending =>
                    "Your registration is waiting for approval. You'll be able to sign in once the Jama Go team accepts it.",
                ClientRegistrationStatus.Rejected =>
                    "Your registration was not approved. Please contact Jama Go for details.",
                // A deactivated staff or VIP account: unchanged, deliberately vague.
                _ => "Invalid email or password.",
            });
        }

        return TypedResult<LoginResponse>.Success(_tokenGenerator.Generate(user));
    }
}
