namespace Jama.Domain.Entities;

/// <summary>
/// The proforma invoice of a quotation — one per quotation. Its number (PI-JG-00001) and date are fixed the first time
/// it is made, so every later download is the same document; the client's P.O. number and date can be added or
/// corrected. The lines, prices and totals are always the quotation's own.
/// </summary>
public class ProformaInvoice : BaseEntity
{
    /// <summary>"PI-JG-00001".</summary>
    public string Number { get; set; } = string.Empty;

    public Guid BoqId { get; set; }
    public Boq Boq { get; set; } = null!;

    /// <summary>The proforma's date: the Doha day it was first made.</summary>
    public DateOnly IssuedOn { get; set; }

    public Guid IssuedById { get; set; }
    public string IssuedByName { get; set; } = string.Empty;

    /// <summary>
    /// Customer details changed for this proforma only. Null means "as on the quotation", so a detail nobody changed
    /// keeps following the quotation. The quotation itself is never changed from here.
    /// </summary>
    public string? CustomerName { get; set; }
    public string? ProjectName { get; set; }
    public string? ContactNumber { get; set; }
    public string? Location { get; set; }

    /// <summary>The client's purchase order, printed in the notes: "P.O. No. … dated …".</summary>
    public string? PoNumber { get; set; }
    public DateOnly? PoDate { get; set; }

    /// <summary>Optimistic concurrency (PostgreSQL xmin).</summary>
    public uint Version { get; set; }
}
