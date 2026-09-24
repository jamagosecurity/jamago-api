namespace Jama.Application.Common.Interfaces;

/// <summary>
/// Everything the contract document needs from an approved quotation — the
/// handful of fields that vary, on top of the fixed bilingual boilerplate the
/// generator itself carries (warranty, payment split, scope of work, and so
/// on, transcribed from the company's reference contract).
/// </summary>
public sealed record ContractPdfModel(
    /// <summary>Printed as both "Contract #" and "Quotation No." — the
    /// reference document reuses the one number for both, and there is no
    /// separate contract numbering sequence.</summary>
    string ContractNumber,
    /// <summary>The date the quotation was approved — the date it became an
    /// agreement, not the date it was first drafted.</summary>
    DateTime ApprovedAt,
    string ProjectName,
    string? SiteLocation,
    string? ClientName,
    string? ContactNumber,
    decimal GrandTotal);

public interface IContractPdfGenerator
{
    byte[] Generate(ContractPdfModel model);
}
