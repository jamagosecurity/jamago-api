namespace Jama.Application.StorageDesigns;

/// <summary>
/// The CCTV storage sizing model, as documented on the admin Storage Sizing
/// page and verified against Dahua's Storage Selector.
///
/// Every figure the workbook produced by hand lives here as one function, so the
/// two sheets that had drifted apart cannot drift again. Where this deliberately
/// disagrees with the workbook it is called out on the method.
///
/// Units are stated in every name because the original's central defect was
/// arithmetic that silently mixed bits with bytes and Mb with MB.
/// </summary>
public static class StorageMath
{
    /// <summary>Bytes in a terabyte, binary — the workbook divides by 1024 twice
    /// from megabytes, so a decimal TB here would move every headline number.</summary>
    private const decimal MegabytesPerTerabyte = 1024m * 1024m;

    private const decimal SecondsPerDay = 86_400m;

    /// <summary>
    /// Storage one camera fills over the retention period, in TB.
    ///
    /// bitrate Mb/s → × 86 400 s → × days → ÷ 8 (bits to bytes) → ÷ 1024² (MB to TB).
    /// The fixture: 2.5 Mbps over 120 days = 3.089905 TB.
    /// </summary>
    public static decimal CameraTerabytes(decimal bitrateMbps, int retentionDays)
    {
        if (bitrateMbps <= 0 || retentionDays <= 0) return 0m;

        var megabits = bitrateMbps * SecondsPerDay * retentionDays;
        return megabits / 8m / MegabytesPerTerabyte;
    }

    /// <summary>
    /// Storage a still-image camera fills, in TB. ANPR records snapshots, not
    /// video, so it is sized from images rather than bitrate.
    ///
    /// The fixture: 2 images × 500 KB × 7 000 events × 120 days = 0.78231 TB.
    /// </summary>
    public static decimal SnapshotTerabytes(
        decimal kilobytesPerImage,
        int imagesPerEvent,
        int eventsPerDay,
        int retentionDays)
    {
        if (kilobytesPerImage <= 0 || imagesPerEvent <= 0 || eventsPerDay <= 0 || retentionDays <= 0)
            return 0m;

        var kilobytes = kilobytesPerImage * imagesPerEvent * eventsPerDay * retentionDays;
        return kilobytes / 1024m / MegabytesPerTerabyte;
    }

    /// <summary>
    /// Usable capacity of one RAID group, in TB.
    ///
    /// Parity disks are subtracted BEFORE the filesystem factor is applied. The
    /// workbook did this on its main sheet but not on the ANPR block, where it
    /// multiplied by total disks and overstated every group by a full disk —
    /// 43.2 TB, not the 50.4 the sheet reports. This is the one place the model
    /// deliberately disagrees with the workbook.
    /// </summary>
    public static decimal GroupTerabytes(
        int totalDisks,
        int parityDisks,
        decimal diskTerabytes,
        decimal filesystemFactor)
    {
        var dataDisks = totalDisks - parityDisks;
        if (dataDisks <= 0 || diskTerabytes <= 0 || filesystemFactor <= 0) return 0m;

        return dataDisks * diskTerabytes * filesystemFactor;
    }

    /// <summary>
    /// How many cameras of a given size a capacity will hold. Floor, not round:
    /// a group with room for 9.8 cameras holds 9.
    /// </summary>
    public static int CameraCeiling(decimal availableTerabytes, decimal perCameraTerabytes)
    {
        if (availableTerabytes <= 0 || perCameraTerabytes <= 0) return 0;
        return (int)Math.Floor(availableTerabytes / perCameraTerabytes);
    }

    /// <summary>Disks needed to hold a volume. Rounds UP — you cannot buy 0.4 of
    /// a disk.</summary>
    public static int DisksNeeded(decimal requiredTerabytes, decimal usablePerDiskTerabytes)
    {
        if (requiredTerabytes <= 0 || usablePerDiskTerabytes <= 0) return 0;
        return (int)Math.Ceiling(requiredTerabytes / usablePerDiskTerabytes);
    }

    /// <summary>
    /// Storage to keep a shorter failover copy of some cameras, in TB.
    ///
    /// The workbook expressed this as an unexplained "÷ 4", which only ever meant
    /// 30 days out of 120. Both periods are inputs here.
    /// The fixture: 9 cameras × 3.0899 TB × (30 ÷ 120) = 6.95 TB.
    /// </summary>
    public static decimal FailoverTerabytes(
        int cameras,
        decimal perCameraTerabytes,
        int failoverDays,
        int retentionDays)
    {
        if (cameras <= 0 || perCameraTerabytes <= 0 || failoverDays <= 0 || retentionDays <= 0)
            return 0m;

        return cameras * perCameraTerabytes * (failoverDays / (decimal)retentionDays);
    }

    /// <summary>Rounds to two places for display. Kept in one place so the
    /// printed lines always add up to the printed total.</summary>
    public static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Four places, for the per-camera figure the whole model rests on —
    /// rounding 3.0899 to 3.09 shifts a 100-camera job by a third of a terabyte.</summary>
    public static decimal RoundPrecise(decimal value) =>
        Math.Round(value, 4, MidpointRounding.AwayFromZero);
}
