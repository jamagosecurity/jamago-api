using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Enums;

namespace Jama.Application.Drawings;

/// <summary>Mirrors BoqVisibility — see there for the reasoning. Visibility is
/// the only access boundary; anyone who can see a drawing may download its
/// files at any status, and Watermark is what keeps a pre-approval PDF from
/// passing as the finished one.</summary>
internal static class DrawingVisibility
{
    internal static bool CanSee(DrawingStatus status, Guid preparedById, ICurrentUser actor) =>
        status == DrawingStatus.Approved
        || preparedById == actor.UserId
        || actor.Has(Permissions.DrawingApprove);

    /// <summary>Whether a served PDF should carry the "DRAFT — NOT APPROVED"
    /// stamp. A DWG/DXF/ZIP can never carry one — see PdfWatermarker — so
    /// those stay blocked pre-approval regardless of who is asking; there is
    /// no unmarked copy of a file that cannot be marked.</summary>
    internal static bool Watermark(DrawingStatus status) => status != DrawingStatus.Approved;
}
