namespace Jama.Application.Common.Interfaces;

/// <summary>
/// Everything the contract document needs from an approved quotation. The
/// legal wording itself — clauses, payment schedule, warranty, and so on —
/// is not modelled as fields here; it is <see cref="Body"/>, resolved by the
/// caller from Boq.ContractText or IContractTextTemplate's default, exactly
/// the way GetBoqPdfQueryHandler resolves a terms override before handing
/// IBoqPdfGenerator a finished string rather than a flag to branch on.
/// </summary>
public sealed record ContractPdfModel(
    /// <summary>Printed as both "Contract #" and "Quotation No." — the
    /// reference document reuses the one number for both, and there is no
    /// separate contract numbering sequence.</summary>
    string ContractNumber,
    /// <summary>Starts at 1 — see Boq.RevisionNumber.</summary>
    int RevisionNumber,
    /// <summary>The date the quotation was approved — the date it became an
    /// agreement, not the date it was first drafted.</summary>
    DateTime ApprovedAt,
    /// <summary>The full Arabic contract text, clause headings and all — see
    /// IContractTextTemplate.BuildDefault for the format this is expected to
    /// follow, and ContractPdfGenerator.ComposeBody for how it is rendered.</summary>
    string Body);

public interface IContractPdfGenerator
{
    byte[] Generate(ContractPdfModel model);
}
