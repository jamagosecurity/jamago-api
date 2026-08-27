namespace Jama.Domain.Enums;

/// <summary>
/// Where a bill of quantities stands. Stored as the enum name, like every other
/// enum here.
/// </summary>
public enum BoqStatus
{
    /// <summary>Being put together by staff. The only state still editable.</summary>
    Draft,
    /// <summary>Handed to an administrator for review.</summary>
    Submitted,
    Approved,
    Rejected,
}
