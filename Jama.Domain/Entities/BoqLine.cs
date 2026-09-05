using Jama.Domain.Enums;

namespace Jama.Domain.Entities;

/// <summary>
/// One measured line.
///
/// The item's name, model, brand, unit and rate are COPIED from the catalogue
/// when the line is written, not read through <see cref="CameraId"/>. A BOQ is a
/// record of what was specified at a point in time: repricing a stock item must
/// not silently restate a bill someone has already approved. CameraId survives
/// only as a soft link, and is null once that item is gone.
///
/// The recording profile is copied on the same terms, so storage sized from this
/// bill answers the same today as on the day it was quoted.
/// </summary>
public class BoqLine : BaseEntity
{
    public Guid BoqSectionId { get; set; }
    public BoqSection Section { get; set; } = null!;

    public Guid? CameraId { get; set; }
    public Camera? Camera { get; set; }

    public string ItemName { get; set; } = string.Empty;
    public string? ModelNo { get; set; }
    public string? Brand { get; set; }

    /// <summary>Form factor as the catalogue held it — "Dome", "Bullet PTZ".
    /// Copied like the brand and model beside it, so a bill still says what was
    /// specified after the stock item is re-typed or retired.</summary>
    public string? Type { get; set; }

    public UnitOfMeasurement Uom { get; set; }
    public decimal Quantity { get; set; }

    // ===== Recording profile =====
    //
    // Copied for the same reason the rate is: storage sizing has to be
    // reproducible. Read live through CameraId, the same BOQ would size
    // differently once someone edited the stock item's bitrate — and size not at
    // all once that item was deleted and the link went null. A figure submitted
    // to MOI cannot quietly change after it was submitted.
    //
    // Both stay nullable/Unspecified: most of a bill is cable, brackets and
    // racks, which have no recording profile and never will.

    public CameraResolution Resolution { get; set; } = CameraResolution.Unspecified;

    /// <summary>Recording bitrate in Mbps as the catalogue held it when this line
    /// was written. Null for anything that is not a camera, and for a camera whose
    /// profile nobody has filled in yet.</summary>
    public decimal? BitrateMbps { get; set; }

    /// <summary>
    /// The rate this line is priced at, and what prints on the document.
    ///
    /// Defaults to <see cref="CatalogueRate"/>. Anyone building a quotation may
    /// send a different one — a negotiated price, a job-specific discount — and
    /// the catalogue figure is kept beside it so the change stays visible.
    /// </summary>
    public decimal UnitRate { get; set; }

    /// <summary>
    /// What the catalogue said when this line was written, whether or not anyone
    /// overrode it.
    ///
    /// Held separately rather than overwritten so a discount stays visible as a
    /// variance. Replacing the list price with the agreed one loses the fact
    /// that a decision was ever made: nobody can see afterwards that a line went
    /// out under cost, and the same bill re-priced from stock would silently
    /// disagree with the copy the client holds.
    ///
    /// Equal to UnitRate on an ordinary line, which is the common case and reads
    /// as "no discount" rather than as missing data.
    /// </summary>
    public decimal CatalogueRate { get; set; }

    public decimal LineTotal { get; set; }
    public int SortOrder { get; set; }
}
