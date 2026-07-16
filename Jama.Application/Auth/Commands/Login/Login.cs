using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
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
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || !user.IsActive || !_passwordHasher.Verify(user, request.Password!))
        {
            return TypedResult<LoginResponse>.Failure("Invalid email or password.");
        }

        return TypedResult<LoginResponse>.Success(_tokenGenerator.Generate(user));
    }
}
