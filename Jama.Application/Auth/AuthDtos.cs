namespace Jama.Application.Auth;

public record LoginRequest(string Email, string Password);

public record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserSummaryDto User);

public record UserSummaryDto(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    /// <summary>
    /// Effective permission keys, already including the implicit set an Admin holds.
    /// Mirrors the permission claims in the token so the client can drive navigation
    /// without decoding the JWT. Never the sole gate — endpoints enforce separately.
    /// </summary>
    IReadOnlyList<string> Permissions);
