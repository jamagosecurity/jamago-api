using FluentValidation;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Profiles;

/// <summary>What a profile screen shows: the name, the designation they wrote, and a picture version (0 = no picture).</summary>
public sealed record ProfileDto(Guid UserId, string FullName, string? Designation, long PhotoVersion, string? Phone = null);

public static class ProfileRules
{
    public const int DesignationMaxLength = 100;
    public const long PhotoMaxBytes = 5L * 1024 * 1024;
    public static readonly string[] PhotoExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public static long PhotoVersion(AdminUser? user) =>
        user is { PhotoKey: not null, PhotoUpdatedAt: { } at }
            ? new DateTimeOffset(DateTime.SpecifyKind(at, DateTimeKind.Utc)).ToUnixTimeSeconds()
            : 0;

    public static ProfileDto ToDto(AdminUser user) => new(user.Id, user.FullName, user.Designation, PhotoVersion(user), user.Phone);

    /// <summary>"+974 5555 1234", "55551234", "+974-5555-1234": digits, spaces and dashes, an optional leading +, 7 to 15 digits.</summary>
    public static bool IsPhone(string value)
    {
        var trimmed = value.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\+?[0-9][0-9 \-]*[0-9]$")) return false;
        var digits = trimmed.Count(char.IsDigit);
        return digits is >= 7 and <= 15;
    }

    /// <summary>A person edits their own profile; an administrator may edit anyone's.</summary>
    public static bool CanEdit(ICurrentUser actor, Guid userId) =>
        actor.UserId == userId || actor.Role == Roles.Admin;

    public const string NotAllowed = "You can only change your own profile.";
}

// ===== Designation =====

public sealed record UpdateProfileCommand(Guid UserId, string? Designation, string? Phone = null) : IRequest<ApiResult<ProfileDto>>;

public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.Designation)
            .MaximumLength(ProfileRules.DesignationMaxLength)
            .WithMessage($"Designation must be {ProfileRules.DesignationMaxLength} characters or fewer.")
            .Must(value => value is null || !value.Any(char.IsControl))
            .WithMessage("Designation cannot contain line breaks.");
        RuleFor(x => x.Phone)
            .MaximumLength(30).WithMessage("Phone number must be 30 characters or fewer.")
            .Must(value => string.IsNullOrWhiteSpace(value) || ProfileRules.IsPhone(value))
            .WithMessage("Enter a valid phone number, like +974 5555 1234.");
    }
}

public sealed class UpdateProfileCommandHandler(IApplicationDbContext context, ICurrentUser actor, TimeProvider timeProvider)
    : IRequestHandler<UpdateProfileCommand, ApiResult<ProfileDto>>
{
    public async Task<ApiResult<ProfileDto>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        if (!ProfileRules.CanEdit(actor, request.UserId))
            return ApiResult<ProfileDto>.Failure(ProfileRules.NotAllowed);

        var user = await context.AdminUsers.FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken);
        if (user is null) return ApiResult<ProfileDto>.Failure("Account not found.");

        var designation = request.Designation?.Trim();
        user.Designation = string.IsNullOrEmpty(designation) ? null : designation;
        var phone = request.Phone?.Trim();
        user.Phone = string.IsNullOrEmpty(phone) ? null : phone;
        user.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<ProfileDto>.Success(ProfileRules.ToDto(user));
    }
}

// ===== Picture =====

public sealed record SetProfilePhotoCommand : IRequest<ApiResult<ProfileDto>>
{
    public Guid UserId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public Stream Content { get; init; } = Stream.Null;
}

public sealed class SetProfilePhotoCommandValidator : AbstractValidator<SetProfilePhotoCommand>
{
    public SetProfilePhotoCommandValidator()
    {
        RuleFor(x => x.FileName)
            .Must(name => ProfileRules.PhotoExtensions.Contains(Path.GetExtension(Path.GetFileName(name ?? string.Empty)).ToLowerInvariant()))
            .WithMessage($"Accepted picture types: {string.Join(", ", ProfileRules.PhotoExtensions)}.");
        RuleFor(x => x.SizeBytes)
            .GreaterThan(0).WithMessage("The picture is empty.")
            .LessThanOrEqualTo(ProfileRules.PhotoMaxBytes).WithMessage("Pictures must be 5 MB or smaller.");
    }
}

public sealed class SetProfilePhotoCommandHandler(
    IApplicationDbContext context, IFileStorage storage, ICurrentUser actor, TimeProvider timeProvider)
    : IRequestHandler<SetProfilePhotoCommand, ApiResult<ProfileDto>>
{
    public async Task<ApiResult<ProfileDto>> Handle(SetProfilePhotoCommand request, CancellationToken cancellationToken)
    {
        if (!ProfileRules.CanEdit(actor, request.UserId))
            return ApiResult<ProfileDto>.Failure(ProfileRules.NotAllowed);

        var user = await context.AdminUsers.FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken);
        if (user is null) return ApiResult<ProfileDto>.Failure("Account not found.");

        var extension = Path.GetExtension(Path.GetFileName(request.FileName)).ToLowerInvariant();

        // The file's own first bytes must match what it claims to be, before anything is written.
        var header = new byte[FileSignatures.HeaderLength];
        var read = await request.Content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        if (!FileSignatures.Matches(extension, header.AsSpan(0, read)))
            return ApiResult<ProfileDto>.Failure($"That file is not a valid {extension} picture.");
        if (!request.Content.CanSeek)
            return ApiResult<ProfileDto>.Failure("The upload could not be read. Please try again.");
        request.Content.Seek(0, SeekOrigin.Begin);

        var contentType = extension switch { ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg" };
        var newKey = $"profiles/{user.Id}/{Guid.CreateVersion7()}{extension}";
        await storage.SaveAsync(request.Content, newKey, cancellationToken);

        var oldKey = user.PhotoKey;
        user.PhotoKey = newKey;
        user.PhotoContentType = contentType;
        user.PhotoUpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        user.UpdatedAt = user.PhotoUpdatedAt;
        await context.SaveChangesAsync(cancellationToken);

        // Only once the new one is safely recorded does the old file go.
        if (!string.IsNullOrEmpty(oldKey)) await storage.DeleteAsync(oldKey, cancellationToken);

        return ApiResult<ProfileDto>.Success(ProfileRules.ToDto(user));
    }
}

public sealed record RemoveProfilePhotoCommand(Guid UserId) : IRequest<ApiResult<ProfileDto>>;

public sealed class RemoveProfilePhotoCommandHandler(
    IApplicationDbContext context, IFileStorage storage, ICurrentUser actor, TimeProvider timeProvider)
    : IRequestHandler<RemoveProfilePhotoCommand, ApiResult<ProfileDto>>
{
    public async Task<ApiResult<ProfileDto>> Handle(RemoveProfilePhotoCommand request, CancellationToken cancellationToken)
    {
        if (!ProfileRules.CanEdit(actor, request.UserId))
            return ApiResult<ProfileDto>.Failure(ProfileRules.NotAllowed);

        var user = await context.AdminUsers.FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken);
        if (user is null) return ApiResult<ProfileDto>.Failure("Account not found.");

        var key = user.PhotoKey;
        user.PhotoKey = null;
        user.PhotoContentType = null;
        user.PhotoUpdatedAt = null;
        user.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrEmpty(key)) await storage.DeleteAsync(key, cancellationToken);
        return ApiResult<ProfileDto>.Success(ProfileRules.ToDto(user));
    }
}

public sealed record ProfilePhotoFile(Stream Content, string ContentType);

/// <summary>Any signed-in user may see a colleague's picture — it appears beside their name in lists.</summary>
public sealed record GetProfilePhotoQuery(Guid UserId) : IRequest<ApiResult<ProfilePhotoFile>>;

public sealed class GetProfilePhotoQueryHandler(IApplicationDbContext context, IFileStorage storage)
    : IRequestHandler<GetProfilePhotoQuery, ApiResult<ProfilePhotoFile>>
{
    public async Task<ApiResult<ProfilePhotoFile>> Handle(GetProfilePhotoQuery request, CancellationToken cancellationToken)
    {
        var user = await context.AdminUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken);
        if (user?.PhotoKey is null) return ApiResult<ProfilePhotoFile>.Failure("No picture.");

        var stream = await storage.OpenReadAsync(user.PhotoKey, cancellationToken);
        return stream is null
            ? ApiResult<ProfilePhotoFile>.Failure("No picture.")
            : ApiResult<ProfilePhotoFile>.Success(new ProfilePhotoFile(stream, user.PhotoContentType ?? "image/jpeg"));
    }
}
