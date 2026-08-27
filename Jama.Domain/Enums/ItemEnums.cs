namespace Jama.Domain.Enums;

/// <summary>
/// Pick-lists for a stock item. All stored as their enum NAME, matching the
/// convention set by <see cref="CameraType"/>: a row reads "AccessControl" in
/// psql, and adding or reordering a member never remaps existing rows.
/// </summary>
public enum ProductCategory
{
    Cctv,
    AccessControl,
    Alarm,
    Network,
    Cable,
    Storage,
    Monitor,
    PowerSupply,
    Accessory,
    Other,
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
