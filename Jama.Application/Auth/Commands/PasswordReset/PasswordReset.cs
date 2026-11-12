using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Application.Options;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jama.Application.Auth.Commands.PasswordReset;

/// <summary>
/// "Forgot password": emails a one-time link to set a new password. The answer is the same whether or not the address
/// belongs to an account, so the form cannot be used to find out who works here. The code is random, stored only as a hash,
/// works once, and expires after <see cref="PasswordResetRules.LinkLifetime"/>.
/// </summary>
public sealed record RequestPasswordResetCommand(string? Email) : IRequest<ApiResult<string>>;

public static class PasswordResetRules
{
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(30);
    public const string Sent = "If that email belongs to an account, a link to set a new password is on its way. It works once, for 30 minutes.";
    public const string BadLink = "This link is not valid any more. Ask for a new one from the sign-in page.";

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}

public sealed class RequestPasswordResetCommandHandler(
    IApplicationDbContext context,
    IMailSender mail,
    IBackgroundWork background,
    IOptions<MailSettings> mailOptions,
    TimeProvider timeProvider) : IRequestHandler<RequestPasswordResetCommand, ApiResult<string>>
{
    public async Task<ApiResult<string>> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email))
            return ApiResult<string>.Failure("Enter your email address.");

        var account = mailOptions.Value.Accounts.FirstOrDefault(a => a.IsConfigured);
        if (account is null)
            return ApiResult<string>.Failure("Password reset by email is not available yet. Ask an administrator to set a new password for you.");

        var user = await context.AdminUsers.FirstOrDefaultAsync(x => x.Email.ToLower() == email, cancellationToken);
        if (user is null || !user.IsActive)
            return ApiResult<string>.Success(PasswordResetRules.Sent);

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        user.PasswordResetTokenHash = PasswordResetRules.Hash(token);
        user.PasswordResetExpiresAt = timeProvider.GetUtcNow().UtcDateTime.Add(PasswordResetRules.LinkLifetime);
        await context.SaveChangesAsync(cancellationToken);

        var link = $"{mailOptions.Value.PublicSiteUrl.TrimEnd('/')}/admin/reset-password?email={Uri.EscapeDataString(user.Email)}&token={token}";
        var name = System.Net.WebUtility.HtmlEncode(user.FullName);
        var text = $"Hello {user.FullName},\n\nSomeone asked to reset the password for your Jama Go account. If it was you, open this link to set a new one (it works once, for 30 minutes):\n\n{link}\n\nIf it was not you, ignore this email — your password has not changed.\n\nJama Go Security";
        var html = $"""
            <div style="font-family:Arial,Helvetica,sans-serif;max-width:560px;margin:0 auto;color:#14305f">
              <div style="padding:22px 26px;background:#0e244e;border-bottom:3px solid #b5b4a8;color:#fff;font-size:12px;letter-spacing:.2em;text-transform:uppercase;font-weight:bold">Jama Go · Password reset</div>
              <div style="padding:26px;border:1px solid #e3e1d8;border-top:0">
                <p style="font-size:16px">Hello {name},</p>
                <p style="font-size:15px;line-height:1.6">Someone asked to reset the password for your Jama Go account. If it was you, set a new one here. The link works once, for 30 minutes.</p>
                <p style="margin:26px 0"><a href="{link}" style="display:inline-block;padding:14px 28px;border-radius:999px;background:#14305f;color:#fff;text-decoration:none;font-weight:bold">Set a new password</a></p>
                <p style="font-size:13px;color:#6b7280;line-height:1.6">If it was not you, ignore this email. Your password has not changed.</p>
              </div>
            </div>
            """;
        var to = user.Email;
        var key = account.Key;

        // Sent after the answer goes back, so how long the mail server takes cannot reveal whether the address exists.
        background.Enqueue(ct => mail.SendAsync(
            new MailSendRequest(key, "Jama Go", [to], [], "Reset your Jama Go password", text, null, null, html), ct));

        return ApiResult<string>.Success(PasswordResetRules.Sent);
    }
}

public sealed record ResetPasswordCommand(string? Email, string? Token, string? NewPassword) : IRequest<ApiResult<string>>;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(v => v.Email).NotEmpty().WithMessage(PasswordResetRules.BadLink);
        RuleFor(v => v.Token).NotEmpty().WithMessage(PasswordResetRules.BadLink);
        // Same strength as changing it while signed in.
        RuleFor(v => v.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must include an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must include a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must include a number.");
    }
}

public sealed class ResetPasswordCommandHandler(IApplicationDbContext context, IPasswordHasher hasher, TimeProvider timeProvider)
    : IRequestHandler<ResetPasswordCommand, ApiResult<string>>
{
    public async Task<ApiResult<string>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email!.Trim().ToLowerInvariant();
        var user = await context.AdminUsers.FirstOrDefaultAsync(x => x.Email.ToLower() == email, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (user?.PasswordResetTokenHash is null || user.PasswordResetExpiresAt is null || user.PasswordResetExpiresAt < now || !user.IsActive)
            return ApiResult<string>.Failure(PasswordResetRules.BadLink);

        var given = Encoding.ASCII.GetBytes(PasswordResetRules.Hash(request.Token!.Trim()));
        var stored = Encoding.ASCII.GetBytes(user.PasswordResetTokenHash);
        if (!CryptographicOperations.FixedTimeEquals(given, stored))
            return ApiResult<string>.Failure(PasswordResetRules.BadLink);

        user.PasswordHash = hasher.Hash(user, request.NewPassword!);
        user.PasswordResetTokenHash = null;      // one use only
        user.PasswordResetExpiresAt = null;
        user.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<string>.Success("Your password has been changed. You can sign in with it now.");
    }
}
