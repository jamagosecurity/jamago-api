namespace Jama.Application.Options;

public class AdminSeedSettings
{
    public const string SectionName = "AdminSeed";

    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = "Administrator";

    /// <summary>
    /// Whether <paramref name="email"/> is the seeded root account — the one
    /// identity allowed to take actions that destroy data outright.
    ///
    /// The single definition of "super administrator": the authorization policy
    /// and the user summary sent to the console both call this, so the button the
    /// operator sees and the gate the server applies cannot disagree. Returns
    /// false when no seed email is configured, so a misconfigured deployment
    /// grants nobody the capability rather than everybody.
    /// </summary>
    public bool IsSuperAdmin(string? email) =>
        !string.IsNullOrWhiteSpace(Email)
        && !string.IsNullOrWhiteSpace(email)
        && string.Equals(email, Email, StringComparison.OrdinalIgnoreCase);
}
