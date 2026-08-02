using Jama.Domain.Enums;

namespace Jama.Application.VipClients;

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
