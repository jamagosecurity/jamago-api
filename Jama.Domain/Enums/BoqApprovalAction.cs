namespace Jama.Domain.Enums;

/// <summary>
/// One step in a quotation's approval trail. Stored as the enum name, like every
/// other enum here.
///
/// Deliberately not the same type as <see cref="BoqStatus"/>. A status is where a
/// document stands now and gets overwritten; an action is something a person did
/// on a date, and never changes afterwards — a quotation rejected, reworked and
/// approved has to be able to say all three.
/// </summary>
public enum BoqApprovalAction
{
    Created,
    /// <summary>Handed to an approver. Recorded on every submission, so a
    /// re-submission after a rejection reads as its own step.</summary>
    Submitted,
    Approved,
    Rejected,
}
