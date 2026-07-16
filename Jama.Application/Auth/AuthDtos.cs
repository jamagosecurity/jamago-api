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
    string Role);
