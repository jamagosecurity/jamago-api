using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Enums;

namespace Jama.Application.Drawings;

/// <summary>Mirrors BoqVisibility — see there for the reasoning. A drawing's
/// files carry the same leak as a quotation's PDF: nothing used to stop a
/// builder downloading a DWG or plotted PDF straight out of a Draft.</summary>
internal static class DrawingVisibility
{
    internal static bool CanSee(DrawingStatus status, Guid preparedById, ICurrentUser actor) =>
        status == DrawingStatus.Approved
        || preparedById == actor.UserId
        || actor.Has(Permissions.DrawingApprove);

    internal static bool CanDownload(DrawingStatus status, ICurrentUser actor) =>
        status == DrawingStatus.Approved || actor.Has(Permissions.DrawingApprove);

    /// <summary>Whether a served PDF should carry the "DRAFT — NOT APPROVED"
    /// stamp. Only meaningful for the one file type that can be stamped at
    /// all — see PdfWatermarker; a DWG/DXF/ZIP stays behind the hard block
    /// in CanDownload with no partial preview.</summary>
    internal static bool Watermark(DrawingStatus status) => status != DrawingStatus.Approved;
}
