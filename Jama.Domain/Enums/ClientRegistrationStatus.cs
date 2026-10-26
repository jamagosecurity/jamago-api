namespace Jama.Domain.Enums;

/// <summary>Where a self-registered client stands. Only Approved opens the
/// login — Pending and Rejected both leave the account switched off.</summary>
public enum ClientRegistrationStatus
{
    Pending,
    Approved,
    Rejected,
}
