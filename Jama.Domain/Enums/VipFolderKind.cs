namespace Jama.Domain.Enums;

/// <summary>
/// The fixed set of folders every VIP client project gets on creation. Stored on
/// the folder row so a rename never breaks the meaning, and so new kinds can be
/// added later without guessing from the display name.
/// </summary>
public enum VipFolderKind
{
    ClientInput,
    QuoteInvoice,
    DsaDocs,
    DiaDocs,
}
