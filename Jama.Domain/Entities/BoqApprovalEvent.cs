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
/// The actor's name is COPIED rather than read through <see cref="ActorId"/>, so
/// the history survives the staff account being removed. When the same account is
/// renamed (or its email changes), the copy is brought up to date so the person
/// shows under their current name — see Jama.Application.Boqs.BoqNames.
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
