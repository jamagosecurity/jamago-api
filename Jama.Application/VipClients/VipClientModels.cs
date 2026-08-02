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
/// The folders every VIP project is created with. Single source of truth: the
/// create handler seeds from here, so adding a folder kind later means adding
/// one line rather than hunting through handlers.
/// </summary>
public static class VipFolders
{
    public static readonly IReadOnlyList<(VipFolderKind Kind, string Name)> Defaults =
    [
        (VipFolderKind.ClientInput, "Client input"),
        (VipFolderKind.QuoteInvoice, "Quote & Invoice"),
        (VipFolderKind.DsaDocs, "DSA Docs"),
        (VipFolderKind.DiaDocs, "DIA Docs"),
    ];

    /// <summary>Default main-folder name when the admin does not supply one.</summary>
    public static string BuildFolderName(string clientName, string projectName) =>
        $"{clientName.Trim()} - {projectName.Trim()}";
}
