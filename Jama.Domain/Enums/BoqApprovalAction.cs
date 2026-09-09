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
    /// <summary>Saved while sitting Rejected — whoever built it, reworking it
    /// before sending it back. Recorded so the approver (and anyone else
    /// watching) can see exactly how many passes the rework took and when
    /// each one landed, not just that a rejected quotation eventually came
    /// back submitted. Distinct from <see cref="Amended"/>: this is the
    /// ordinary author making an ordinary edit, not the super administrator
    /// reopening something already decided.</summary>
    Revised,
    /// <summary>Changed after a decision had been taken on it. Only the super
    /// administrator can do this, and it is recorded rather than silent: an
    /// approval that refers to lines which have since moved must not look
    /// untouched.</summary>
    Amended,
}
