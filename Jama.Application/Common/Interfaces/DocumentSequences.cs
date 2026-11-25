namespace Jama.Application.Common.Interfaces;

/// <summary>Database sequences that number documents. A number taken by a save that then fails is not reused, so there can
/// be gaps; there are never duplicates.</summary>
public static class DocumentSequences
{
    public const string TravelClaimNumbers = "TravelClaimNumbers";
    public const string ReimbursementNumbers = "ReimbursementNumbers";
    public const string ProformaNumbers = "ProformaNumbers";

    public static readonly string[] All = [TravelClaimNumbers, ReimbursementNumbers, ProformaNumbers];
}
