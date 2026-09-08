namespace Jama.Application.Common;

/// <summary>
/// Policy names that are not derived from <see cref="Permissions"/>.
///
/// Permissions answer "what may you do once you are in your portal", and every
/// Admin holds all of them. That makes them the wrong tool for an action no
/// ordinary administrator should reach, so those get a policy of their own.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// The single seeded root administrator, identified by AdminSeed:Email rather
    /// than by role — there is exactly one root account and the role list is
    /// deliberately coarse. Guards actions that destroy data outright.
    /// </summary>
    public const string SuperAdmin = "superadmin";

    /// <summary>
    /// Reading quotations, satisfied by EITHER the build grant or the approve
    /// grant.
    ///
    /// An approver has to open the document to decide on it, and building
    /// quotations is not part of that job. Without this the only way to let
    /// somebody approve would be to also let them write — which is the division
    /// the approve grant exists to make.
    ///
    /// Composite, so deliberately not in <see cref="Permissions.All"/>: it is
    /// not a thing an admin ticks, it is two things that both open the door.
    /// </summary>
    public const string BoqRead = "boq.read";

    /// <summary>Reading drawings, satisfied by EITHER drawing.manage or
    /// drawing.approve — an approver has to open the document to decide on it.
    /// Mirrors <see cref="BoqRead"/>.</summary>
    public const string DrawingRead = "drawing.read";
}
