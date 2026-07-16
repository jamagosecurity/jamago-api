namespace Jama.Application.Contacts;

public record ContactSubmissionDto(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    string Service,
    string Message,
    string Status,
    DateTime CreatedAt);
