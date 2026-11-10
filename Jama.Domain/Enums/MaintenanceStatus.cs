namespace Jama.Domain.Enums;

/// <summary>Where a maintenance ticket stands. Stored as the enum name.</summary>
public enum MaintenanceStatus
{
    /// <summary>Logged, not yet picked up.</summary>
    Open,
    /// <summary>Assigned and being worked.</summary>
    InProgress,
    Resolved,
    /// <summary>Withdrawn without being worked — a duplicate report, or the
    /// issue resolved itself. Distinct from Resolved: nobody fixed anything.</summary>
    Cancelled,
}
