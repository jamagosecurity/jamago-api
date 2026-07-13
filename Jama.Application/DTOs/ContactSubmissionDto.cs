namespace Jama.Application.DTOs;

public record ContactSubmissionDto(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    string Service,
    string Message,
    string Status,
    DateTime CreatedAt);

public record CreateContactSubmissionRequest(
    string FullName,
    string Email,
    string? Phone,
    string Service,
    string Message);
