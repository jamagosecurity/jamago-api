using Jama.Application.Common;
using Jama.Application.Common.Interfaces;

namespace Jama.Application.Maintenance;

/// <summary>Who may see one ticket: whoever filed it, whoever it is
/// assigned to, or anyone holding maintenance.manage (admins implicitly).
/// Mirrors BoqVisibility/UberTripVisibility's shape.</summary>
internal static class MaintenanceVisibility
{
    internal static bool CanSee(Guid reportedById, Guid? assignedToId, ICurrentUser actor) =>
        reportedById == actor.UserId
        || assignedToId == actor.UserId
        || actor.Has(Permissions.MaintenanceManage);
}
