using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Enums;

namespace Jama.Application.Boqs;

/// <summary>
/// Who may see a quotation, before it has been decided on.
///
/// Visibility is the boundary that matters: anyone who can see a row may also
/// download it, at any status — a plain builder included. What stops an
/// unapproved quotation passing for a finished one is not a download block
/// but <see cref="Watermark"/>, which is why that rule stays separate and
/// unconditional rather than being folded in here.
/// </summary>
internal static class BoqVisibility
{
    /// <summary>
    /// Whether this account may see the row at all.
    ///
    /// Approved is public to anyone who can read the module. Before that, a
    /// quotation is visible only to whoever built it, whoever may approve it,
    /// or an administrator — not to every other builder browsing the list. A
    /// half-finished quotation nobody chose to submit yet is nobody else's
    /// business.
    /// </summary>
    internal static bool CanSee(BoqStatus status, Guid preparedById, ICurrentUser actor) =>
        status == BoqStatus.Approved
        || preparedById == actor.UserId
        || actor.Has(Permissions.BoqApprove);

    /// <summary>Whether the PDF served for this status should carry the
    /// "DRAFT — NOT APPROVED" stamp. Everything before Approved does. This is
    /// the whole of what keeps a pre-approval download from passing as the
    /// finished document — deliberately kept independent of who is asking.</summary>
    internal static bool Watermark(BoqStatus status) => status != BoqStatus.Approved;
}
