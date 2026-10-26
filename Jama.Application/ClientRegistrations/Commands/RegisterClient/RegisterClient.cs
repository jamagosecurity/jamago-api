using FluentValidation;
using Jama.Application.Auth;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.ClientRegistrations.Commands.RegisterClient;

/// <summary>A client signing themselves up on the website. Anonymous — the
/// endpoint is rate-limited, and the account it creates stays switched off
/// until an admin accepts it.</summary>
public sealed record RegisterClientCommand : IRequest<ApiResult<string>>
{
    public string? FullName { get; init; }
    public string? Email { get; init; }
    public string? Mobile { get; init; }
    public string? SiteLocation { get; init; }
    public string? Password { get; init; }
}

public sealed class RegisterClientCommandValidator : AbstractValidator<RegisterClientCommand>
{
    public RegisterClientCommandValidator()
    {
        // Stop at the first failure per field: an empty mobile is "required",
        // not also "invalid" and "too few digits" on top of it.
        RuleFor(x => x.FullName).Cascade(CascadeMode.Stop)
            .Must(NotBlank).WithMessage("Name is required.")
            .MaximumLength(150).WithMessage("Name must be 150 characters or fewer.");

        RuleFor(x => x.Email).Cascade(CascadeMode.Stop)
            .Must(NotBlank).WithMessage("Email is required.")
            .MaximumLength(256).WithMessage("Email must be 256 characters or fewer.")
            .EmailAddress().WithMessage("Enter a valid email address.");

        // Qatari numbers are written with spaces ("+974 3139 2007"), so the
        // shape is checked loosely; the digit count is what actually rules out
        // a typo. 7–15 covers a bare local number up to the E.164 maximum.
        RuleFor(x => x.Mobile).Cascade(CascadeMode.Stop)
            .Must(NotBlank).WithMessage("Mobile number is required.")
            .MaximumLength(40).WithMessage("Mobile number must be 40 characters or fewer.")
            .Matches(@"^\+?[0-9\s\-()]+$").WithMessage("Enter a valid mobile number.")
            .Must(m => m!.Count(char.IsDigit) is >= 7 and <= 15)
            .WithMessage("Enter a valid mobile number.");

        RuleFor(x => x.SiteLocation).Cascade(CascadeMode.Stop)
            .Must(NotBlank).WithMessage("Site location is required.")
            .MaximumLength(300).WithMessage("Site location must be 300 characters or fewer.");

        RuleFor(x => x.Password).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Password is required.");
        PasswordRules.Strong(RuleFor(x => x.Password))
            .When(x => !string.IsNullOrEmpty(x.Password));
    }

    private static bool NotBlank(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class RegisterClientCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider)
    : IRequestHandler<RegisterClientCommand, ApiResult<string>>
{
    private const string Received =
        "Registration received. You'll be able to sign in once the Jama Go team approves it.";

    public async Task<ApiResult<string>> Handle(RegisterClientCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email!.Trim().ToLowerInvariant();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var existing = await context.AdminUsers
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (existing is not null)
        {
            var previous = await context.ClientRegistrations
                .FirstOrDefaultAsync(r => r.AdminUserId == existing.Id, cancellationToken);

            // A turned-down registration can be sent again — someone who
            // mistyped their site address should not need a second email
            // address to fix it. Anything else on this email is a real account
            // (or one already in the queue), and stays exactly as it is.
            if (previous?.Status == ClientRegistrationStatus.Pending)
                return ApiResult<string>.Failure(
                    "A registration with this email is already waiting for approval.");

            if (previous?.Status != ClientRegistrationStatus.Rejected || existing.IsActive)
                return ApiResult<string>.Failure(
                    "An account with this email already exists. Sign in instead.");

            Apply(previous, request, email);
            previous.Status = ClientRegistrationStatus.Pending;
            previous.Category = null;
            previous.Teams = [];
            previous.ReviewedById = null;
            previous.ReviewedByName = null;
            previous.ReviewedAt = null;
            previous.RejectionReason = null;
            previous.UpdatedAt = now;

            existing.FullName = previous.FullName;
            existing.PasswordHash = passwordHasher.Hash(existing, request.Password!);
            existing.UpdatedAt = now;

            await context.SaveChangesAsync(cancellationToken);
            return ApiResult<string>.Success(Received);
        }

        var account = new AdminUser
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            FullName = request.FullName!.Trim(),
            Role = Roles.Client,
            // Off until an admin accepts — login refuses an inactive account,
            // and tells this one why (see LoginCommandHandler).
            IsActive = false,
            CreatedAt = now,
        };
        account.PasswordHash = passwordHasher.Hash(account, request.Password!);

        var registration = new ClientRegistration
        {
            Id = Guid.CreateVersion7(),
            AdminUserId = account.Id,
            Account = account,
            CreatedAt = now,
        };
        Apply(registration, request, email);

        context.AdminUsers.Add(account);
        context.ClientRegistrations.Add(registration);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two sign-ups racing on the same email: the account's unique index
            // is the real guard, and the loser simply finds it taken.
            return ApiResult<string>.Failure(
                "An account with this email already exists. Sign in instead.");
        }

        return ApiResult<string>.Success(Received);
    }

    private static void Apply(ClientRegistration target, RegisterClientCommand request, string email)
    {
        target.FullName = request.FullName!.Trim();
        target.Email = email;
        target.Mobile = request.Mobile!.Trim();
        target.SiteLocation = request.SiteLocation!.Trim();
    }
}
