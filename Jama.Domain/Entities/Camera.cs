using Jama.Domain.Enums;

namespace Jama.Domain.Entities;

/// <summary>
/// A line in the stock inventory.
///
/// Started life as a camera-only record and grew into a general stock item —
/// the table and routes keep the "camera" name because that is where the data
/// and the URLs already are, but an item here may be any product or service.
///
/// Distinct from <see cref="CameraDetail"/>, which records the cameras found on
/// a client site during a quarterly technician inspection. This is stock.
/// </summary>
public class Camera : BaseEntity
{
    // ===== Identity =====

    /// <summary>What the item is called on a quote or invoice. The only required
    /// descriptive field.</summary>
    public string ItemName { get; set; } = string.Empty;

    public string Brand { get; set; } = string.Empty;
    /// <summary>
    /// The form factor, as free text — "Dome", "Bullet PTZ", "Thermal ANPR".
    ///
    /// Was a fixed enum. The list could never cover what suppliers actually
    /// ship, and an item that did not fit one of seven names had to be filed
    /// under the wrong one. It reaches the client and the BOQ exactly as typed.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Manufacturer model number, e.g. "DS-2CD2143G0-I". Optional, and stored as
    /// an empty string rather than null when it is not known: it forms part of
    /// the unique key with Brand and Type, and Postgres treats NULLs as distinct,
    /// so nullable would let unlimited "brand + type, no model" duplicates in.
    /// </summary>
    public string ModelNo { get; set; } = string.Empty;

    public ProductCategory Category { get; set; }

    /// <summary>Extra words to match on — trade names, part numbers, common
    /// misspellings — so an item is findable by more than its own name.</summary>
    public string? SearchKey { get; set; }

    /// <summary>
    /// Free-text notes, held in both languages. Neither is part of the unique
    /// key, so null is fine, and either may be filled without the other — a line
    /// described only in Arabic is as valid as one described only in English.
    /// </summary>
    public string? DescriptionEn { get; set; }

    public string? DescriptionAr { get; set; }

    // ===== Pricing & inventory =====

    /// <summary>What the supplier charges, per unit, in QAR.</summary>
    public decimal? SupplierCost { get; set; }

    /// <summary>Mark-up on the supplier cost, as a percentage.</summary>
    public decimal? Margin { get; set; }

    /// <summary>
    /// Selling price per unit, in QAR. Stored rather than derived from cost and
    /// margin: a price is often a round number agreed with the customer, and
    /// recomputing it would quietly overwrite that with 187.43.
    /// </summary>
    public decimal? Rate { get; set; }

    public decimal? Discount { get; set; }

    public int Quantity { get; set; }

    /// <summary>Quantity at or below which the item counts as running out.</summary>
    public int? LowStock { get; set; }

    /// <summary>Harmonised System code used on customs and tax paperwork.</summary>
    public string? HsnCode { get; set; }

    public UnitOfMeasurement Uom { get; set; }

    // ===== Recording profile =====
    // Only meaningful for cameras, and only used by the storage calculator. Both
    // stay optional: a cable or a bracket has neither, and a camera whose
    // profile nobody has filled in yet must still be sellable.

    public CameraResolution Resolution { get; set; } = CameraResolution.Unspecified;

    /// <summary>Recording bitrate in Mbps — the figure storage is sized from.
    /// Entered rather than derived: it depends on codec, frame rate and scene
    /// complexity, and the installer knows the profile they will configure.</summary>
    public decimal? BitrateMbps { get; set; }

    /// <summary>Tax percentage applied to this item. Null means "No Taxation" —
    /// a distinct state from 0%, which is a rate that was deliberately chosen.</summary>
    public decimal? TaxRate { get; set; }

    public ItemType ItemType { get; set; }

    // ===== Warranty =====

    /// <summary>Length of the warranty, counted in <see cref="WarrantyUnit"/>.
    /// Both are null together when no warranty is recorded.</summary>
    public int? WarrantyValue { get; set; }

    public WarrantyUnit? WarrantyUnit { get; set; }

    public string? Notes { get; set; }

    public ICollection<CameraImage> Images { get; set; } = [];
}
