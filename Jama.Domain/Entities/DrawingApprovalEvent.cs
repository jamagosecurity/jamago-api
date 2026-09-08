using Jama.Domain.Enums;

namespace Jama.Domain.Entities;

/// <summary>
/// One entry in a drawing's approval history: who did what to it, when, and —
/// for a rejection — why.
///
/// Append-only, on the same terms as BoqApprovalEvent. Nothing here is ever
/// updated or deleted, including when a rejected drawing is revised and
/// approved: the point of the trail is that the rejection still shows, with the
/// reason it was given. The decision fields on <see cref="Drawing"/> answer
/// "where does this stand"; these rows answer "how did it get here".
/// </summary>
public class DrawingApprovalEvent : BaseEntity
{
    public Guid DrawingId { get; set; }
    public Drawing Drawing { get; set; } = null!;

    public DrawingApprovalAction Action { get; set; }

    public Guid ActorId { get; set; }
    public string? ActorName { get; set; }

    /// <summary>Why it was rejected, as the approver wrote it. Null on every
    /// other action.</summary>
    public string? Reason { get; set; }
}
