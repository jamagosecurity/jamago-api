namespace Jama.Domain.Enums;

/// <summary>
/// Pick-lists for a stock item. All stored as their enum NAME, matching the
/// convention the other pick-lists follow: a row reads "AccessControl" in
/// psql, and adding or reordering a member never remaps existing rows.
/// </summary>
/// <summary>
/// What section of a bill of quantities a stock item belongs to.
///
/// These mirror <c>BoqSectionTitles</c> one for one, deliberately: an item's
/// category is what decides which section it lands in, and two lists that were
/// meant to agree but were maintained separately had already drifted.
///
/// Stored as the enum NAME, so the members below can be reordered freely and
/// existing rows still read. Alarm and Other are gone — Other was never used,
/// and the single Alarm item moved to AccessControl, which is where an intruder
/// panel is quoted.
/// </summary>
public enum ProductCategory
{
    Cctv,
    Accessory,
    Storage,
    Monitor,
    Network,
    PowerSupply,

    /// <summary>Key Point of Interest cameras. Broken out from the ordinary
    /// camera count on an MOI submission, which states them separately.</summary>
    Kpoi,

    Cable,
    AccessControl,
}

public enum UnitOfMeasurement
{
    Piece,
    Box,
    Set,
    Metre,
    Roll,
    Pack,
    Pair,
}

/// <summary>
/// Sensor resolution, the input the storage calculator sizes from. Named
/// members rather than a raw number so "4 MP" cannot arrive as 4.0001, and
/// stored as the enum NAME like the rest of the pick-lists.
/// </summary>
public enum CameraResolution
{
    Unspecified,
    OneMp,
    TwoMp,
    ThreeMp,
    FourMp,
    FiveMp,
    SixMp,
    EightMp,
    TwelveMp,
}

public enum ItemType
{
    Product,
    Service,
}

public enum WarrantyUnit
{
    Day,
    Month,
    Year,
}
