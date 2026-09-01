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
/// <summary>One RAID set in the array: what it actually contains.</summary>
/// <param name="Number">1-based, for labelling "Group 1", "Group 2".</param>
/// <param name="AvailableTerabytes">Capacity this set delivers — its data disks
/// after the filesystem factor. The MOI sheet's "Available storage" column.</param>
/// <param name="ProposedTerabytes">Its data disks at label size, before the
/// filesystem factor. The MOI sheet's "Proposed Storage" column, which counts
/// only data disks — parity and hot spares are added once at the summary as
/// "including RAID + Hotspare".</param>
public sealed record RaidGroup(
    int Number,
    int DataDisks,
    int ParityDisks,
    int TotalDisks,
    decimal AvailableTerabytes,
    decimal ProposedTerabytes);

/// <summary>
/// An array sized from a requirement: what to buy, not what to check.
/// </summary>
/// <param name="DataDisks">Disks holding data. These are what the capacity comes from.</param>
/// <param name="ParityDisks">Disks given over to parity, across every group.</param>
/// <param name="HotSpareDisks">Standby disks, powered but idle, that a controller
/// rebuilds onto automatically when a member fails. They carry no data and add no
/// capacity — they buy back the hours between a disk failing and someone driving
/// to site, which on a 120-day retention is when the footage is at risk.</param>
/// <param name="TotalDisks">What is actually purchased — data, parity and hot spares.</param>
/// <param name="Groups">RAID groups the disks are split across.</param>
/// <param name="DisksPerGroup">Members in each RAID set, hot spares excluded.</param>
/// <param name="UsableTerabytes">Capacity the data disks provide, after the filesystem factor.</param>
/// <param name="RawTerabytes">Every disk bought at its label size, before parity,
/// hot spares or the filesystem factor. The purchase-order figure.</param>
public sealed record RaidArray(
    int DataDisks,
    int ParityDisks,
    int HotSpareDisks,
    int TotalDisks,
    int Groups,
    int DisksPerGroup,
    decimal UsableTerabytes,
    decimal RawTerabytes,
    /// <summary>The groups laid out one by one. Data disks are spread as evenly
    /// as they divide, so a remainder lands in the last group rather than being
    /// implied by an average nobody can act on.</summary>
    IReadOnlyList<RaidGroup> Layout);

/// <summary>
/// One disk size costed out: what the array looks like if built from it.
/// </summary>
/// <param name="OverBuyTerabytes">Usable capacity beyond what was asked for. Buying
/// 80 TB to hold 9.5 TB is not a safety margin, it is a shelf of disks nobody costed.</param>
public sealed record ArrayOption(
    decimal DiskTerabytes,
    /// <summary>RAID level this option was costed at.</summary>
    int RaidLevel,
    RaidArray Array,
    decimal OverBuyTerabytes,
    /// <summary>What the disks alone cost, when a price was supplied.</summary>
    decimal? DiskCost,
    /// <summary>What the enclosures cost — one per RAID group, when a price was supplied.</summary>
    decimal? EnclosureCost,
    /// <summary>Disks plus enclosures. Null when no price was supplied at all.</summary>
    decimal? TotalCost);

/// <summary>A disk the buyer can actually order: a size, and what it costs.</summary>
public sealed record DiskCandidate(decimal Terabytes, decimal? PricePerDisk = null);

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
    /// Smallest group a RAID level will form.
    ///
    /// RAID-1 is a mirrored PAIR — one data disk and its copy — so two is a
    /// complete set. RAID-5 and RAID-6 stripe, and striping across a single data
    /// disk is not striping, so they need one more than their parity count plus
    /// one: 3 and 4.
    ///
    /// This matters far more than it looks. Treating RAID-1 as parity+2 forced a
    /// 3-disk minimum on jobs whose whole requirement fits one disk, and with a
    /// hot spare that made every small job buy four disks to hold 3 TB.
    /// </summary>
    public static int MinimumGroupDisks(int raidLevel, int parityDisks) =>
        raidLevel == 1 ? 2 : parityDisks + 2;

    /// <summary>
    /// The array a requirement actually needs: how many disks, in how many groups.
    ///
    /// This is the calculation the workbook never did. It sized a proposed array
    /// and reported whether it covered the need — which answers "is 8 disks
    /// enough?" but not "how many disks do I buy?", the only question anyone
    /// specifying a job is actually asking. Eight 16 TB disks against a 9.5 TB
    /// requirement reported 1,061% coverage, which reads as a healthy design and
    /// is really a shelf of disks nobody costed.
    ///
    /// Parity holds no data, so the data-disk count does not move as groups are
    /// added: this settles in one pass rather than iterating. What parity does
    /// change is the total bought, which is why the two are reported separately.
    ///
    /// <paramref name="baysPerGroup"/> is the enclosure limit. Groups are formed
    /// as full as the chassis allows and the remainder goes in the last one,
    /// which is then padded up to the RAID minimum if it landed under it.
    /// </summary>
    public static RaidArray RecommendArray(
        decimal requiredTerabytes,
        decimal diskTerabytes,
        decimal filesystemFactor,
        int parityDisks,
        int baysPerGroup,
        int hotSpareDisks = 0,
        int raidLevel = 5)
    {
        var usablePerDisk = diskTerabytes * filesystemFactor;

        // A RAID-1 set is exactly two disks whatever the chassis holds: one disk
        // and its mirror. More capacity means more PAIRS, not a wider set.
        var dataDisksPerGroup = raidLevel == 1 ? 1 : baysPerGroup - parityDisks;

        if (requiredTerabytes <= 0 || usablePerDisk <= 0 || dataDisksPerGroup <= 0)
            return new RaidArray(0, 0, 0, 0, 0, 0, 0m, 0m, []);

        var dataDisks = (int)Math.Ceiling(requiredTerabytes / usablePerDisk);
        var groups = (int)Math.Ceiling((decimal)dataDisks / dataDisksPerGroup);
        var parity = groups * parityDisks;

        // A single group holding one data disk is arithmetically sufficient and
        // not a RAID set. Pad to the level's minimum so what is quoted is the
        // thing that was asked for.
        var minimumMembers = MinimumGroupDisks(raidLevel, parityDisks) * groups;
        if (dataDisks + parity < minimumMembers)
            dataDisks = minimumMembers - parity;

        // Hot spares sit outside the RAID set: bought and racked, holding nothing
        // until a member dies, so they never appear in usable capacity.
        //
        // GLOBAL, not per group. One spare on a controller covers every group it
        // serves — a disk failing in group 1 or group 2 is rebuilt onto the same
        // standby. Multiplying by group count bought a disk per group that nothing
        // asked for. Only split groups across separate enclosures need a spare
        // each, and that is a decision for whoever specifies the enclosures.
        var hotSpares = Math.Max(0, hotSpareDisks);
        var members = dataDisks + parity;
        var totalDisks = members + hotSpares;

        var usable = dataDisks * usablePerDisk;
        var raw = totalDisks * diskTerabytes;

        // Spread the data disks across the groups. Integer division leaves a
        // remainder, which goes one disk at a time into the earliest groups —
        // so the sizes differ by at most one and every group is a real set
        // somebody can build, rather than an average of "6.5 disks".
        var layout = new List<RaidGroup>(groups);
        var baseData = dataDisks / groups;
        var remainder = dataDisks % groups;

        for (var i = 0; i < groups; i++)
        {
            var groupData = baseData + (i < remainder ? 1 : 0);
            layout.Add(new RaidGroup(
                i + 1,
                groupData,
                parityDisks,
                groupData + parityDisks,
                Round(groupData * usablePerDisk),
                Round(groupData * diskTerabytes)));
        }

        return new RaidArray(
            dataDisks,
            parity,
            hotSpares,
            totalDisks,
            groups,
            (int)Math.Ceiling((decimal)members / groups),
            Round(usable),
            Round(raw),
            layout);
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
    /// <summary>
    /// Sizes the array from every disk size on offer and returns them ranked, best
    /// first.
    ///
    /// Asking the user to pick a disk size first was the same mistake as asking
    /// them to pick a disk count: it is an output, not a preference. A 9.5 TB job
    /// on 16 TB disks buys 80 TB raw to use 9.5 — arithmetically fine, and nobody
    /// would order it if they had seen the 8 TB row beside it.
    ///
    /// Ranked on RAW TERABYTES PURCHASED, which is what an invoice is proportional
    /// to and which penalises both over-buying and parity overhead in one number.
    /// Fewer total disks breaks a tie, because every disk is a bay, a watt and a
    /// thing that can fail.
    /// </summary>
    public static IReadOnlyList<ArrayOption> CompareDiskSizes(
        decimal requiredTerabytes,
        IEnumerable<DiskCandidate> candidates,
        decimal filesystemFactor,
        int parityDisks,
        int baysPerGroup,
        int hotSpareDisks,
        decimal? enclosurePricePerGroup = null,
        int raidLevel = 5)
    {
        var options = new List<ArrayOption>();

        foreach (var candidate in candidates.Where(c => c.Terabytes > 0).DistinctBy(c => c.Terabytes))
        {
            var array = RecommendArray(requiredTerabytes, candidate.Terabytes,
                filesystemFactor, parityDisks, baysPerGroup, hotSpareDisks, raidLevel);

            if (array.TotalDisks == 0) continue;

            // Enclosures are priced per GROUP, and the group count moves with the
            // disk size — which is the whole reason this has to be in the total.
            // Counting disks alone recommended 66 small disks in 6 enclosures over
            // 34 large ones in 3, saving 4,000 on disk and losing 14,400 on chassis.
            var diskCost = candidate.PricePerDisk is { } price
                ? Round(price * array.TotalDisks)
                : (decimal?)null;

            var enclosureCost = enclosurePricePerGroup is { } chassis
                ? Round(chassis * array.Groups)
                : (decimal?)null;

            var total = diskCost is null && enclosureCost is null
                ? (decimal?)null
                : Round((diskCost ?? 0m) + (enclosureCost ?? 0m));

            options.Add(new ArrayOption(
                candidate.Terabytes,
                raidLevel,
                array,
                Round(array.UsableTerabytes - requiredTerabytes),
                diskCost,
                enclosureCost,
                total));
        }

        // Cost decides it when the buyer told us what the disks cost, because that
        // is the only measure that trades "more small disks" against "fewer large
        // ones" honestly. Ranking on capacity bought recommended 107 4 TB disks
        // over 21 22 TB ones to save 8% of terabytes — five times the bays, the
        // power and the things that can fail, for a saving nobody asked for.
        //
        // Without prices there is no honest trade, so the fallback takes the
        // FEWEST DISKS whose over-buy stays within a fifth of the requirement.
        // That is the engineering answer — least hardware that is not wasteful —
        // and it never lands on an absurd disk count.
        var priced = options.Where(o => o.TotalCost is not null).ToList();
        if (priced.Count == options.Count && options.Count > 0)
        {
            return options
                .OrderBy(o => o.TotalCost)
                .ThenBy(o => o.Array.TotalDisks)
                .ToList();
        }

        var ceiling = requiredTerabytes * 0.2m;
        return options
            .OrderBy(o => o.OverBuyTerabytes <= ceiling ? 0 : 1)
            .ThenBy(o => o.Array.TotalDisks)
            .ThenBy(o => o.Array.RawTerabytes)
            .ToList();
    }

    /// <summary>
    /// Weighs every disk size at every RAID level allowed, ranked best first.
    ///
    /// The RAID level is as much an outcome as the disk size. A 2.35 TB stills
    /// volume is cheapest mirrored — two disks and a spare — while striping it
    /// forces RAID-5's three-member minimum and buys a fourth disk for capacity
    /// nothing will ever use. A 314 TB recording array is the reverse: mirroring
    /// doubles every disk, where one parity disk per group would do.
    ///
    /// One level for the whole job therefore over-buys at one end or the other.
    /// A caller who must have a particular level still passes it and gets it —
    /// compliance is a reason to fix the level, and that decision is theirs.
    /// </summary>
    public static IReadOnlyList<ArrayOption> CompareLevelsAndDisks(
        decimal requiredTerabytes,
        IEnumerable<DiskCandidate> candidates,
        decimal filesystemFactor,
        int baysPerGroup,
        int hotSpareDisks,
        decimal? enclosurePricePerGroup,
        IEnumerable<int> raidLevels)
    {
        var disks = candidates.ToList();
        var all = new List<ArrayOption>();

        foreach (var level in raidLevels.Distinct())
        {
            var parity = level == 6 ? 2 : 1;
            all.AddRange(CompareDiskSizes(requiredTerabytes, disks, filesystemFactor,
                parity, baysPerGroup, hotSpareDisks, enclosurePricePerGroup, level));
        }

        var priced = all.Count(o => o.TotalCost is not null);
        if (priced == all.Count && all.Count > 0)
            return all.OrderBy(o => o.TotalCost).ThenBy(o => o.Array.TotalDisks).ToList();

        var ceiling = requiredTerabytes * 0.2m;
        return all
            .OrderBy(o => o.OverBuyTerabytes <= ceiling ? 0 : 1)
            .ThenBy(o => o.Array.TotalDisks)
            .ThenBy(o => o.Array.RawTerabytes)
            .ToList();
    }

}
