namespace Jama.Application.Proformas;

/// <summary>The fixed wording of every proforma invoice — printed on the PDF and shown on screen from this one place.</summary>
public static class ProformaText
{
    public static readonly IReadOnlyList<string> Terms =
    [
        "All amounts are in Qatari Riyals (QAR).",
        "All terms and conditions apply in accordance with the quotation approved by both parties.",
    ];

    public const string CompanyName = "JAMA GO SECURITY EQUIPMENT";
    public const string CompanyAddress =
        "Unit 41, Zone 56, Street 340, Building No 349, Al Ain Complex, Salwa Road, Doha – Qatar";
    public const string CompanyMobiles = "+974 3064 4006 | +974 3139 5879";

    private static readonly Dictionary<string, string> UnitNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Piece"] = "Pcs",
        ["Metre"] = "Mtr",
    };

    /// <summary>The unit as the proforma prints it: Pcs, Mtr, Box, Set, Roll, Lot.</summary>
    public static string Unit(string uom) => UnitNames.TryGetValue(uom, out var name) ? name : uom;
}
