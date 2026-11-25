using Jama.Domain.Enums;

namespace Jama.Application.Proformas;

/// <summary>A quotation as the administrator's list shows it, with its proforma if one was made.</summary>
public sealed record AdminQuotationDto(
    Guid Id,
    string BoqNumber,
    int RevisionNumber,
    string ProjectName,
    string? SiteLocation,
    string? ClientName,
    string? ContactNumber,
    DateOnly IssueDate,
    BoqStatus Status,
    decimal GrandTotal,
    string? PreparedByName,
    DateTime CreatedAt,
    ProformaDto? Proforma);

/// <summary>The kept part of a proforma invoice: its number and date, the client's P.O., and any customer details
/// changed for this proforma (null = as on the quotation).</summary>
public sealed record ProformaDto(
    string Number,
    DateOnly IssuedOn,
    string IssuedByName,
    string? PoNumber,
    DateOnly? PoDate,
    string? CustomerName,
    string? ProjectName,
    string? ContactNumber,
    string? Location);

/// <summary>Customer details as typed on screen for a proforma. Empty means "as on the quotation".</summary>
public sealed record ProformaCustomer(string? Name, string? Project, string? ContactNumber, string? Location)
{
    public static readonly ProformaCustomer AsQuotation = new(null, null, null, null);
}
