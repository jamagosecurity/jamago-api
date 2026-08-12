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
}
