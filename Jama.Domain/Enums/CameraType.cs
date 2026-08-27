namespace Jama.Domain.Enums;

/// <summary>
/// The physical form factor of a camera, as the inventory tracks it.
///
/// Stored as a string (see CameraConfiguration) and serialised as a string, so
/// the value in the database and the value on the wire are the same word and
/// adding a member never renumbers the existing rows.
/// </summary>
public enum CameraType
{
    Bullet,
    Dome,
    Ptz,
    Fisheye,
    Thermal,
    Anpr,
    Box,
}
