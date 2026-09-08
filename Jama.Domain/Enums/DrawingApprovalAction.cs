namespace Jama.Domain.Enums;

/// <summary>
/// One step in a drawing's approval trail. Stored as the enum name.
///
/// Deliberately not DrawingStatus. A status is where a document stands now and
/// gets overwritten; an action is something a person did on a date, and never
/// changes afterwards — a drawing rejected, revised and approved has to be able
/// to say all three.
/// </summary>
public enum DrawingApprovalAction
{
    Created,
    /// <summary>Handed to an approver. Recorded on every submission, so a
    /// re-submission after a rejection reads as its own step.</summary>
    Submitted,
    Approved,
    Rejected,
    /// <summary>Changed after a decision had been taken on it. Only the super
    /// administrator can do this, and it is recorded rather than silent.</summary>
    Amended,
}
