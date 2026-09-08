using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Jama.Domain.Enums;

namespace Jama.Application.Drawings;

/// <summary>
/// The rules about where a drawing may go next, and the trail it leaves.
/// Mirrors BoqWorkflow — the shape that turned out right for one approval
/// workflow in this app turned out right for the other, but the two are kept as
/// separate copies rather than one shared generic: a rule changed for
/// quotations must not silently reach drawings, and vice versa.
/// </summary>
internal static class DrawingWorkflow
{
    /// <summary>
    /// Whether the drawing and its files may still be changed.
    ///
    /// Everything up to a decision is open: a draft, one waiting in an
    /// approver's queue, and one that came back rejected. An APPROVAL is what
    /// closes it — a statement about a particular set of files — so only the
    /// super administrator may edit past that point, and the amendment is
    /// recorded.
    /// </summary>
    internal static bool IsEditable(DrawingStatus status) =>
        status is not DrawingStatus.Approved;

    internal static bool CanSubmit(DrawingStatus status) =>
        status is DrawingStatus.Draft or DrawingStatus.Rejected;

    /// <summary>Only a drawing actually waiting on someone.</summary>
    internal static bool CanDecide(DrawingStatus status) => status is DrawingStatus.Submitted;

    /// <summary>
    /// Records a step. Appended, never updated: a drawing that was rejected,
    /// revised and approved has to be able to show all three.
    /// </summary>
    internal static void Record(
        IApplicationDbContext context,
        Drawing drawing,
        DrawingApprovalAction action,
        ICurrentUser actor,
        DateTime now,
        string? reason = null)
    {
        context.DrawingApprovalEvents.Add(new DrawingApprovalEvent
        {
            Id = Guid.CreateVersion7(),
            DrawingId = drawing.Id,
            Action = action,
            ActorId = actor.UserId,
            ActorName = actor.DisplayName,
            Reason = reason,
            CreatedAt = now,
        });
    }

    /// <summary>
    /// Puts a drawing back in front of an approver. The previous rejection is
    /// cleared from the document — it is no longer where the document stands —
    /// but stays in the history.
    /// </summary>
    internal static void Submit(Drawing drawing, DateTime now)
    {
        drawing.Status = DrawingStatus.Submitted;
        drawing.SubmittedAt = now;
        drawing.RejectedById = null;
        drawing.RejectedByName = null;
        drawing.RejectedAt = null;
        drawing.RejectionReason = null;
    }

    internal static void Approve(Drawing drawing, ICurrentUser actor, DateTime now)
    {
        drawing.Status = DrawingStatus.Approved;
        drawing.ApprovedById = actor.UserId;
        drawing.ApprovedByName = actor.DisplayName;
        drawing.ApprovedAt = now;
    }

    internal static void Reject(Drawing drawing, ICurrentUser actor, DateTime now, string reason)
    {
        drawing.Status = DrawingStatus.Rejected;
        drawing.RejectedById = actor.UserId;
        drawing.RejectedByName = actor.DisplayName;
        drawing.RejectedAt = now;
        drawing.RejectionReason = reason;
        // An earlier approval is cleared: the document is not approved any more.
        drawing.ApprovedById = null;
        drawing.ApprovedByName = null;
        drawing.ApprovedAt = null;
    }
}
