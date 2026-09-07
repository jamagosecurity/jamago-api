using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Jama.Domain.Enums;

namespace Jama.Application.Boqs;

/// <summary>
/// The rules about where a quotation may go next, and the trail it leaves.
///
/// Held in one place because they are enforced from four handlers and read from
/// two screens: a transition allowed by the submit handler but not by the editor
/// is a button that fails, and one allowed by the editor but not the handler is a
/// button that lies. The endpoints decide WHO may act — that is authorization,
/// and it belongs in the policy — while this decides whether the act makes sense
/// for the document as it stands.
/// </summary>
internal static class BoqWorkflow
{
    /// <summary>
    /// Whether the lines and figures may still be changed.
    ///
    /// Everything up to a decision is open: a draft, a quotation waiting in an
    /// approver's queue, and one that came back rejected. Until somebody has
    /// answered it, a quotation is still being written, and the person writing it
    /// may correct a rate or a quantity as many times as the job needs without
    /// withdrawing it and sending it again.
    ///
    /// An APPROVAL is what closes it. That is a statement about a particular set
    /// of lines, and editing them afterwards would leave the statement attached
    /// to a document nobody agreed to — so only the super administrator may, and
    /// the amendment is recorded.
    /// </summary>
    internal static bool IsEditable(BoqStatus status) =>
        status is not BoqStatus.Approved;

    internal static bool CanSubmit(BoqStatus status) =>
        status is BoqStatus.Draft or BoqStatus.Rejected;

    /// <summary>Only a quotation actually waiting on someone. Approving one twice
    /// is a double-click, not a decision.</summary>
    internal static bool CanDecide(BoqStatus status) => status is BoqStatus.Submitted;

    /// <summary>
    /// Records a step. Appended, never updated: a quotation that was rejected,
    /// reworked and approved has to be able to show all three.
    /// </summary>
    internal static void Record(
        IApplicationDbContext context,
        Boq boq,
        BoqApprovalAction action,
        ICurrentUser actor,
        DateTime now,
        string? reason = null)
    {
        context.BoqApprovalEvents.Add(new BoqApprovalEvent
        {
            Id = Guid.CreateVersion7(),
            BoqId = boq.Id,
            Action = action,
            // From the token, never the request — who decided is not something a
            // caller gets to assert. The name is copied so the trail still reads
            // after the account is renamed or removed.
            ActorId = actor.UserId,
            ActorName = actor.DisplayName,
            Reason = reason,
            CreatedAt = now,
        });
    }

    /// <summary>
    /// Puts a quotation back in front of an approver.
    ///
    /// The previous rejection is cleared from the document because it is no
    /// longer where the document stands — it stays in the history, which is the
    /// part that is not allowed to forget.
    /// </summary>
    internal static void Submit(Boq boq, DateTime now)
    {
        boq.Status = BoqStatus.Submitted;
        boq.SubmittedAt = now;
        boq.RejectedById = null;
        boq.RejectedByName = null;
        boq.RejectedAt = null;
        boq.RejectionReason = null;
    }

    internal static void Approve(Boq boq, ICurrentUser actor, DateTime now)
    {
        boq.Status = BoqStatus.Approved;
        boq.ApprovedById = actor.UserId;
        boq.ApprovedByName = actor.DisplayName;
        boq.ApprovedAt = now;
    }

    internal static void Reject(Boq boq, ICurrentUser actor, DateTime now, string reason)
    {
        boq.Status = BoqStatus.Rejected;
        boq.RejectedById = actor.UserId;
        boq.RejectedByName = actor.DisplayName;
        boq.RejectedAt = now;
        boq.RejectionReason = reason;
        // An earlier approval is cleared: the document is not approved any more,
        // and leaving the pair on it would let a list show both at once.
        boq.ApprovedById = null;
        boq.ApprovedByName = null;
        boq.ApprovedAt = null;
    }
}
