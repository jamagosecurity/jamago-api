namespace Jama.Application.Common;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
    public const string Technician = "Technician";

    /// <summary>
    /// A VIP client. Sees only their own project folders and nothing else in the
    /// system, so this role is deliberately outside the staff hierarchy rather
    /// than a weaker Staff.
    /// </summary>
    public const string Client = "Client";
}
