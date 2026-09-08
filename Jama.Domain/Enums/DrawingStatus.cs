namespace Jama.Domain.Enums;

/// <summary>
/// Where a drawing stands. Stored as the enum name, like every other enum here.
///
/// Its own type rather than reusing BoqStatus: the two workflows read alike
/// today, but a drawing is a standalone document with no quotation behind it,
/// and giving it a shared enum would mean a change meant for one silently
/// reaching the other.
/// </summary>
public enum DrawingStatus
{
    /// <summary>Being put together. The only state files may be freely added or
    /// removed in.</summary>
    Draft,
    /// <summary>Handed to the drawing approver for review.</summary>
    Submitted,
    Approved,
    Rejected,
}
