namespace Jama.Application.Common.Interfaces;

/// <summary>
/// The handful of fields the default contract wording is built from. Everything
/// else in the document is fixed legal boilerplate the template itself carries.
/// </summary>
public sealed record ContractTextModel(
    /// <summary>Printed as the quotation reference inside the contract body.</summary>
    string ContractNumber,
    /// <summary>Starts at 1 — see Boq.RevisionNumber.</summary>
    int RevisionNumber,
    /// <summary>The date the quotation was approved — the date it became an
    /// agreement, not the date it was first drafted.</summary>
    DateTime ContractDate,
    string ProjectName,
    string? SiteLocation,
    string? ClientName,
    decimal GrandTotal);

/// <summary>
/// Builds the default Arabic contract wording a quotation's approval pre-fills
/// the editable contract screen with. Staff type over this default — see
/// Boq.ContractText — so it is also what a contract falls back to generating
/// on the spot if nobody ever has.
/// </summary>
public interface IContractTextTemplate
{
    string BuildDefault(ContractTextModel model);
}
