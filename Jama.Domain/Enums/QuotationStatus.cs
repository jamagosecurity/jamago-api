namespace Jama.Domain.Enums;

/// <summary>
/// Where a quotation stands. Stored as the enum name, like every other enum
/// here, so a row reads "Accepted" in psql.
/// </summary>
public enum QuotationStatus
{
    /// <summary>Being written. The only state in which it is not yet a promise.</summary>
    Draft,
    Sent,
    Accepted,
    Rejected,
    /// <summary>Past its validity date without an answer.</summary>
    Expired,
}
