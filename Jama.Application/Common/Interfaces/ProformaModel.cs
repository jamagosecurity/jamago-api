namespace Jama.Application.Common.Interfaces;

/// <summary>
/// The proforma invoice of a quotation as it prints — the one model the PDF, Excel and Word files are all made from,
/// so the three always agree.
/// </summary>
/// <param name="Number">"PI-JG-00001", or null for a draft preview of a quotation that is not approved yet.</param>
/// <param name="Date">The proforma's date: the day it was first made (today, for a draft preview).</param>
/// <param name="Ref">Who prepared the quotation, as their account names them today.</param>
public sealed record ProformaModel(
    string? Number,
    DateOnly Date,
    string QuoteNumber,
    DateOnly QuoteDate,
    string? Ref,
    string? ClientName,
    string ProjectName,
    string? ContactNumber,
    string? Location,
    IReadOnlyList<ProformaModelSection> Sections,
    decimal Subtotal,
    decimal Discount,
    decimal NetTotal,
    string? PoNumber,
    DateOnly? PoDate,
    bool Draft);

public sealed record ProformaModelSection(string Title, IReadOnlyList<ProformaModelLine> Lines);

/// <param name="Number">Running number across the whole document: 1, 2, 3 … not restarting per section.</param>
/// <param name="Unit">The unit of measurement's name: Piece, Box, Set, Metre, Roll, Lot.</param>
public sealed record ProformaModelLine(
    int Number,
    string Description,
    string? Brand,
    string? Model,
    string Unit,
    decimal Quantity,
    decimal UnitPrice,
    decimal Total);
