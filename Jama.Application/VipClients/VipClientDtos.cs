using Jama.Domain.Enums;

namespace Jama.Application.VipClients;

public sealed record VipClientListItemDto(
    Guid Id,
    string ClientName,
    string ProjectName,
    string FolderName,
    string Email,
    bool IsActive,
    bool CanSignIn,
    int DocumentCount,
    DateTime CreatedAt);

public sealed record VipClientDocumentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime UploadedAt,
    string? UploadedBy);

public sealed record VipClientFolderDto(
    Guid Id,
    VipFolderKind Kind,
    string Name,
    int DisplayOrder,
    IReadOnlyList<VipClientDocumentDto> Documents);

public sealed record VipClientDetailDto(
    Guid Id,
    string ClientName,
    string ProjectName,
    string FolderName,
    string Email,
    bool IsActive,
    bool CanSignIn,
    DateTime CreatedAt,
    IReadOnlyList<VipClientFolderDto> Folders);

/// <summary>
/// Resolved content for a download. The handler decides access, not the
/// endpoint, so the admin and client routes enforce the same rule.
/// </summary>
public sealed record VipDocumentContent(Stream Content, string FileName, string ContentType);
