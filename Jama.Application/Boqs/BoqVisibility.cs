using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Enums;

namespace Jama.Application.Boqs;

/// <summary>
/// Who may see a quotation, and who may download the finished document, before
/// it has been decided on.
///
/// The approval workflow used to control editing and who decides, but nothing
/// about the one output that actually matters: the document leaving the
/// building. A builder could create a Draft and download the client-ready PDF
/// immediately — no submission, no approval, nothing in the way. This is the
/// fix: visibility and download are gated on the same two facts, ownership and
/// approval, everywhere a quotation is read.
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

    /// <summary>
    /// Whether this account may download the finished PDF.
    ///
    /// A pure builder — even the document's own author — gets nothing until
    /// Approved. That is deliberate, not stricter than intended: if the owner
    /// could download their own pre-approval copy "just to check formatting",
    /// that download is the exact file they could send to a client, which
    /// defeats the reason this exists. Whoever holds the approve grant may
    /// download at any stage they can see, because they need the real document
    /// to decide on it — but see BoqVisibility.Watermark for what they get.
    /// </summary>
    internal static bool CanDownload(BoqStatus status, ICurrentUser actor) =>
        status == BoqStatus.Approved || actor.Has(Permissions.BoqApprove);

    /// <summary>Whether the PDF served for this status should carry the
    /// "DRAFT — NOT APPROVED" stamp. Everything before Approved does, including
    /// the approver's own preview copy: if it is ever forwarded by mistake, it
    /// is unmistakably unofficial.</summary>
    internal static bool Watermark(BoqStatus status) => status != BoqStatus.Approved;
}
