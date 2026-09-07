using Jama.Domain.Enums;

namespace Jama.Domain.Entities;

/// <summary>
/// One entry in a quotation's approval history: who did what to it, when, and —
/// for a rejection — why.
///
/// Append-only. Nothing here is ever updated or deleted, including when a
/// rejected quotation is reworked and approved: the point of the trail is that
/// the rejection still shows, with the reason it was given. The decision fields
/// on <see cref="Boq"/> answer "where does this stand"; these rows answer "how
/// did it get here".
///
/// The actor's name is COPIED rather than read through <see cref="ActorId"/>, on
/// the same terms as a quotation line's price: a staff account may be renamed or
/// removed, and a history that rewrites itself when it is would be no history at
/// all.
/// </summary>
public class BoqApprovalEvent : BaseEntity
{
    public Guid BoqId { get; set; }
    public Boq Boq { get; set; } = null!;

    public BoqApprovalAction Action { get; set; }

    public Guid ActorId { get; set; }
    public string? ActorName { get; set; }

    /// <summary>Why it was rejected, as the approver wrote it. Null on every
    /// other action — an approval needs no defence, a rejection does.</summary>
    public string? Reason { get; set; }
}
