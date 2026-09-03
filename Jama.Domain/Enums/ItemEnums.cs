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

    /// <summary>Number-plate cameras. Broken out for the same reason KPOI is —
    /// a submission states them apart from the main system — and because they
    /// are sized apart from it too: stills per event rather than continuous
    /// video, on their own array and their own page of the storage sheet.
    ///
    /// They were previously quoted under Cctv and told apart by the free-text
    /// type reading "ANPR". That worked until someone typed the type
    /// differently, at which point a number-plate camera was silently sized as
    /// continuous video. A category cannot be mistyped.</summary>
    Anpr,

    Cable,
    AccessControl,

    /// <summary>Labour rather than hardware — installation, commissioning, an
    /// annual maintenance contract. Quoted as a line like anything else, but
    /// never sized: the storage calculator reads only the camera categories, so
    /// a service line cannot contribute imaginary footage to an array.
    ///
    /// Distinct from <c>ItemType.Service</c>, which says how a line is priced.
    /// This says which section of the document it prints under.</summary>
    Service,
}

/// <summary>
/// How a stock item is counted.
///
/// Stored as the enum NAME, and written on screen and on the document in the
/// abbreviated form the trade uses — "Pcs", "Mtr", "Loc". The two are kept
/// apart deliberately: the printed form is what a client reads and changes
/// with taste, while the stored name is in every existing row and cannot move
/// without a data migration.
///
/// Pack and Pair are gone. Neither was ever quoted here, and a pick-list that
/// offers units nobody uses is one more thing to get wrong on a priced page.
/// </summary>
public enum UnitOfMeasurement
{
    Piece,
    Box,
    Set,
    Metre,
    Roll,

    /// <summary>A whole location, priced as one lump — the unit for work that
    /// is quoted per site rather than per item.</summary>
    Location,
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
