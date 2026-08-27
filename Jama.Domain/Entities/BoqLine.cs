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

    public UnitOfMeasurement Uom { get; set; }
    public decimal Quantity { get; set; }

    /// <summary>Taken from the catalogue by the server. Never accepted from the
    /// client — that is what makes the rate an administrator's to set.</summary>
    public decimal UnitRate { get; set; }

    public decimal LineTotal { get; set; }
    public int SortOrder { get; set; }
}
