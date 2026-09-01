using FluentValidation;
using Jama.Application.Common.Models;
using MediatR;

namespace Jama.Application.StorageDesigns.Queries.CalculateStorageDesign;

// ===== Request =====

/// <summary>A group of identical cameras: how many, at what bitrate.</summary>
public sealed record StorageCameraInput
{
    /// <summary>For labelling the result row only — the maths never reads it.</summary>
    public string? Label { get; init; }

    public int Count { get; init; }
    public decimal BitrateMbps { get; init; }
}

/// <summary>A group of number-plate cameras, sized from snapshots not video.</summary>
public sealed record StorageAnprInput
{
    public string? Label { get; init; }
    public int Count { get; init; }
    public decimal KilobytesPerImage { get; init; } = 500m;
    public int ImagesPerEvent { get; init; } = 2;
    public int EventsPerDay { get; init; } = 7_000;
}

/// <summary>A disk on offer: its size, and what one costs if the caller knows.</summary>
public sealed record DiskCandidateInput
{
    public decimal Terabytes { get; init; }
    public decimal? PricePerDisk { get; init; }
}

/// <summary>One RAID group in the array.</summary>
public sealed record StorageGroupInput
{
    public string? Label { get; init; }
    public int TotalDisks { get; init; }
    public decimal DiskTerabytes { get; init; } = 16m;
}

/// <summary>
/// Everything the sizing needs, in one stateless request. Nothing is stored:
/// a design is a calculation over a quotation, not a record of its own.
/// </summary>
public sealed record CalculateStorageDesignQuery : IRequest<TypedResult<StorageDesignDto>>
{
    /// <summary>Days of footage to keep. One input driving every block — in the
    /// workbook this was hardcoded into each formula.</summary>
    public int RetentionDays { get; init; } = 120;

    /// <summary>
    /// Video redundancy allowance. ONE by default, meaning none applied.
    ///
    /// The workbook used 1.06, where it was mislabelled "capacity loss due to
    /// formatting". The MOI storage sheet applies no such factor at all: its
    /// per-camera figure is bitrate x time and nothing else, which is how 36
    /// cameras at 2.5 Mbps over 120 days come to exactly its stated 111.24 TB.
    ///
    /// Defaulting to 1.06 inflated every submission by 6%, so a reviewer checking
    /// the arithmetic by hand got a different number from the one printed and the
    /// document did not reconcile. Raise it deliberately for engineering headroom;
    /// do not leave it raised on something being submitted.
    /// </summary>
    public decimal Redundancy { get; init; } = 1m;

    /// <summary>Share of a disk left after formatting. 0.90.</summary>
    public decimal FilesystemFactor { get; init; } = 0.90m;

    /// <summary>
    /// RAID level to build at. Null weighs 1, 5 and 6 and takes the cheapest,
    /// which is what most jobs want — a small stills volume is cheapest mirrored
    /// and a large recording array is cheapest striped, so one level for both
    /// over-buys at one end. Set it explicitly when compliance requires a level.
    /// </summary>
    public int? RaidLevel { get; init; }

    /// <summary>Days of footage the failover copy keeps, and how many cameras it
    /// covers. Zero cameras means no failover block.</summary>
    public int FailoverDays { get; init; } = 30;
    public int FailoverCameras { get; init; }

    /// <summary>Disk size for the RECORDING array. Null lets the server choose the
    /// best of <see cref="CandidateDisks"/>, which is what callers should normally
    /// do — the size is an outcome, not a preference.</summary>
    public decimal? RecommendDiskTerabytes { get; init; }

    /// <summary>Disk size for the ANPR array, chosen separately. The two arrays
    /// hold wildly different amounts, so the right disk for one is rarely the
    /// right disk for the other; null lets the server pick for this one too.</summary>
    public decimal? AnprDiskTerabytes { get; init; }

    /// <summary>
    /// Disks the buyer can actually order, and what they cost. Send only what is
    /// stocked, so the tool cannot recommend a disk nobody sells — and send the
    /// prices, because cost is the only measure that trades "more small disks"
    /// against "fewer large ones" honestly.
    /// </summary>
    /// <summary>
    /// What one enclosure costs. Charged per RAID group, because the group count
    /// moves with the disk size — a size that needs twice the groups needs twice
    /// the chassis, and leaving that out picks the wrong disk.
    /// </summary>
    public decimal? EnclosurePricePerGroup { get; init; }

    public IReadOnlyList<DiskCandidateInput> CandidateDisks { get; init; } =
    [
        new() { Terabytes = 4m }, new() { Terabytes = 6m }, new() { Terabytes = 8m },
        new() { Terabytes = 10m }, new() { Terabytes = 12m }, new() { Terabytes = 14m },
        new() { Terabytes = 16m }, new() { Terabytes = 18m }, new() { Terabytes = 20m },
        new() { Terabytes = 22m },
    ];

    /// <summary>Drive bays per enclosure, capping how large one RAID group can
    /// get. Eight is the common 2U chassis.</summary>
    public int BaysPerGroup { get; init; } = 8;

    /// <summary>
    /// Standby disks for the whole array, not per group: a global spare covers
    /// every group on the controller, so two groups do not need two spares. One
    /// is the usual specification — it lets rebuilding start the moment a member
    /// fails rather than when someone reaches the site. Zero is accepted for a
    /// job that genuinely does not want them.
    /// </summary>
    public int HotSpareDisks { get; init; } = 1;

    // ===== MOI submission columns =====
    //
    // The MOI storage sheet states these against every row. Three are
    // descriptive — they explain how the bitrate was arrived at rather than
    // changing it — but a submission is rejected without them, so they travel
    // with the design instead of being written on by hand afterwards.

    /// <summary>Compression the bitrate assumes. Descriptive: the bitrate already
    /// reflects it, so changing this alone must not move a figure.</summary>
    public string RecordingCodec { get; init; } = "H.264";

    /// <summary>Frames per second the bitrate assumes. Descriptive, as above.</summary>
    public int Fps { get; init; } = 15;

    /// <summary>
    /// Share of the day the cameras actually record, as a percentage.
    ///
    /// This one is NOT descriptive. 100 means continuous recording and multiplies
    /// by one; 50 means motion-triggered recording halves the footage and halves
    /// the storage. The sheet states 100 for this project.
    /// </summary>
    public int MotionPercent { get; init; } = 100;

    /// <summary>Drive interface, for the sheet's "Type of HDD" column.</summary>
    public string HddType { get; init; } = "SATA";

    public IReadOnlyList<StorageCameraInput> Cameras { get; init; } = [];
    public IReadOnlyList<StorageAnprInput> Anpr { get; init; } = [];

    /// <summary>
    /// An array the caller wants checked. Optional: the recommendation is
    /// computed regardless, and sizing a job needs no guess to start from.
    /// </summary>
    public IReadOnlyList<StorageGroupInput> Groups { get; init; } = [];
}

// ===== Response =====

public sealed record StorageCameraLineDto(
    string Label,
    int Count,
    decimal BitrateMbps,
    decimal PerCameraTerabytes,
    decimal TerabytesRaw,
    decimal BandwidthMbps);

public sealed record StorageAnprLineDto(
    string Label,
    int Count,
    decimal PerCameraTerabytes,
    decimal Terabytes,
    int DisksNeeded);

public sealed record StorageGroupDto(
    string Label,
    int TotalDisks,
    int ParityDisks,
    int DataDisks,
    decimal DiskTerabytes,
    decimal AvailableTerabytes,
    /// <summary>How many cameras of the design's average size this group holds.</summary>
    int CameraCeiling);

public sealed record StorageDesignDto(
    int RetentionDays,
    decimal Redundancy,
    decimal FilesystemFactor,
    int RaidLevel,

    // ===== MOI submission columns =====
    /// <summary>Compression the bitrate assumes.</summary>
    string RecordingCodec,
    /// <summary>Frames per second the bitrate assumes.</summary>
    int Fps,
    /// <summary>Share of the day recorded. Applied to the footage, not decorative.</summary>
    int MotionPercent,
    /// <summary>Drive interface, for the sheet's "Type of HDD" column.</summary>
    string HddType,

    IReadOnlyList<StorageCameraLineDto> Cameras,
    int CameraCount,
    /// <summary>Before redundancy.</summary>
    decimal VideoTerabytesRaw,
    /// <summary>After redundancy — what the array must actually hold.</summary>
    decimal VideoTerabytes,
    decimal BandwidthMbps,

    IReadOnlyList<StorageAnprLineDto> Anpr,
    decimal AnprTerabytes,

    decimal FailoverTerabytes,
    decimal RequiredTerabytes,

    /// <summary>
    /// The recording array — the answer to "how many disks do I buy?" for the
    /// video, failover copy included. Sized on its own, without the ANPR stills.
    /// </summary>
    StorageRecommendationDto Recommended,

    /// <summary>
    /// A second, separate array for the number-plate stills. All zeros when the
    /// quotation has no ANPR cameras, which the client reads as "do not show".
    /// </summary>
    StorageRecommendationDto RecommendedAnpr,

    /// <summary>Capacity the recording array must hold: video plus failover.</summary>
    decimal VideoRequiredTerabytes,
    /// <summary>Capacity the ANPR array must hold.</summary>
    decimal AnprRequiredTerabytes,

    /// <summary>Every disk size weighed up for the recording array, best first.</summary>
    IReadOnlyList<StorageDiskOptionDto> DiskOptions,

    /// <summary>The same comparison for the ANPR array. Empty when there is none.</summary>
    IReadOnlyList<StorageDiskOptionDto> AnprDiskOptions,

    /// <summary>
    /// One array holding both, for comparison. Separating ANPR pays the RAID
    /// minimum twice, which on a small job costs more than the separation is
    /// worth — this is what the alternative would be. Null when there is no ANPR
    /// to share with, or when separating is not more expensive.
    /// </summary>
    StorageSharedOptionDto? SharedAlternative,

    IReadOnlyList<StorageGroupDto> Groups,
    decimal AvailableTerabytes,
    /// <summary>Available minus required. Negative means the array is short.</summary>
    decimal SurplusTerabytes,
    /// <summary>Available as a percentage of required. 100 or more passes.</summary>
    decimal CoveragePercent,
    bool Covered);

/// <summary>What a single combined array would cost, when that is cheaper.</summary>
public sealed record StorageSharedOptionDto(
    int TotalDisks,
    int Groups,
    decimal DiskTerabytes,
    decimal UsableTerabytes,
    /// <summary>Disks saved by sharing rather than separating.</summary>
    int DisksSaved,
    /// <summary>Money saved, when the disks were priced.</summary>
    decimal? CostSaved);

/// <summary>One disk size weighed up, so the choice is visible and not just asserted.</summary>
public sealed record StorageDiskOptionDto(
    decimal DiskTerabytes,
    int TotalDisks,
    int Groups,
    decimal UsableTerabytes,
    decimal RawTerabytes,
    /// <summary>Usable capacity beyond what was needed.</summary>
    decimal OverBuyTerabytes,
    /// <summary>What the disks alone cost.</summary>
    decimal? DiskCost,
    /// <summary>What the enclosures cost — one per RAID group.</summary>
    decimal? EnclosureCost,
    /// <summary>Disks plus enclosures.</summary>
    decimal? TotalCost,
    /// <summary>True for the one the server picked.</summary>
    bool Chosen);

/// <summary>One RAID set, as it will actually be built.</summary>
public sealed record StorageGroupLayoutDto(
    int Number,
    int DataDisks,
    int ParityDisks,
    int TotalDisks,
    /// <summary>The MOI sheet's "Available storage" for this array — its data
    /// disks after the filesystem factor.</summary>
    decimal AvailableTerabytes,
    /// <summary>The MOI sheet's "Proposed Storage" for this array — its data
    /// disks at label size. Parity and hot spares are added once at the summary,
    /// as "including RAID + Hotspare".</summary>
    decimal ProposedTerabytes);

/// <summary>
/// The array specification: everything a submission has to state about the
/// storage, from one calculation rather than assembled by hand per document.
/// </summary>
public sealed record StorageRecommendationDto(
    /// <summary>
    /// The level THIS array is built at.
    ///
    /// Held per array, not once for the design: the two are weighed separately,
    /// so a small stills volume lands on RAID-1 while the recording array lands
    /// on RAID-5. Reporting one level for both labelled the ANPR array with the
    /// recording array's — on a submission that states resilience per array, that
    /// is a claim about the wrong thing.
    /// </summary>
    int RaidLevel,
    int DataDisks,
    int ParityDisks,
    /// <summary>Standby disks. Bought and racked, holding nothing until a member fails.</summary>
    int HotSpareDisks,
    /// <summary>Disks purchased — data, parity and hot spares.</summary>
    int TotalDisks,
    int Groups,
    /// <summary>Members per RAID set, hot spares excluded.</summary>
    int DisksPerGroup,
    int BaysPerGroup,
    decimal DiskTerabytes,
    /// <summary>Every disk at its label size, before parity, spares or formatting.</summary>
    decimal RawTerabytes,
    /// <summary>Capacity the recommendation delivers, after parity and the filesystem factor.</summary>
    decimal UsableTerabytes,
    /// <summary>Usable minus required. What the array has left once the retention is met.</summary>
    decimal SpareTerabytes,
    /// <summary>Days of footage the usable capacity actually holds at this camera load.</summary>
    int RetentionDaysAchieved,
    /// <summary>The groups one by one, so the reader sees the split rather than
    /// an average. Empty when there is nothing to build.</summary>
    IReadOnlyList<StorageGroupLayoutDto> Layout);

// ===== Validator =====

public sealed class CalculateStorageDesignQueryValidator : AbstractValidator<CalculateStorageDesignQuery>
{
    public CalculateStorageDesignQueryValidator()
    {
        RuleFor(x => x.RetentionDays)
            .InclusiveBetween(1, 3650).WithMessage("Retention must be between 1 and 3650 days.");

        RuleFor(x => x.Redundancy)
            .InclusiveBetween(1m, 3m).WithMessage("Redundancy must be between 1 and 3.");

        RuleFor(x => x.FilesystemFactor)
            .InclusiveBetween(0.5m, 1m).WithMessage("Filesystem factor must be between 0.5 and 1.");

        RuleFor(x => x.RaidLevel)
            .Must(x => x is null or 1 or 5 or 6)
            .WithMessage("RAID level must be 1, 5 or 6.");

        RuleFor(x => x.FailoverDays)
            .InclusiveBetween(0, 3650).WithMessage("Failover retention must be between 0 and 3650 days.");

        RuleFor(x => x.Cameras)
            .NotEmpty().WithMessage("Add at least one camera group.")
            .When(x => x.Anpr.Count == 0);

        RuleForEach(x => x.Cameras).ChildRules(camera =>
        {
            camera.RuleFor(x => x.Count)
                .InclusiveBetween(1, 100_000).WithMessage("Camera count must be between 1 and 100,000.");
            camera.RuleFor(x => x.BitrateMbps)
                .GreaterThan(0m).WithMessage("Every camera group needs a bitrate.")
                .LessThanOrEqualTo(100m).WithMessage("Bitrate must be 100 Mbps or less.");
        });

        RuleForEach(x => x.Anpr).ChildRules(anpr =>
        {
            anpr.RuleFor(x => x.Count).InclusiveBetween(1, 100_000);
            anpr.RuleFor(x => x.KilobytesPerImage).GreaterThan(0m).LessThanOrEqualTo(100_000m);
            anpr.RuleFor(x => x.ImagesPerEvent).InclusiveBetween(1, 100);
            anpr.RuleFor(x => x.EventsPerDay).InclusiveBetween(1, 10_000_000);
        });

        RuleForEach(x => x.Groups).ChildRules(group =>
        {
            // Parity disks are subtracted, so a group must hold more than parity
            // to store anything at all.
            group.RuleFor(x => x.TotalDisks)
                .InclusiveBetween(2, 96).WithMessage("A group must have between 2 and 96 disks.");
            group.RuleFor(x => x.DiskTerabytes)
                .GreaterThan(0m).LessThanOrEqualTo(1000m);
        });
    }
}

// ===== Handler =====

/// <summary>
/// Stateless: no entity, no database, no migration. A sizing is a view over
/// numbers the caller already has, and storing one would immediately go stale
/// against the quotation it was derived from.
/// </summary>
public sealed class CalculateStorageDesignQueryHandler
    : IRequestHandler<CalculateStorageDesignQuery, TypedResult<StorageDesignDto>>
{
    public Task<TypedResult<StorageDesignDto>> Handle(
        CalculateStorageDesignQuery request,
        CancellationToken cancellationToken)
    {
        var days = request.RetentionDays;
        // Levels on offer: the one asked for, or all three to be weighed up.
        int[] levels = request.RaidLevel is { } fixedLevel ? [fixedLevel] : [1, 5, 6];

        // RAID-1 mirrors, so its "parity" disk is the copy — one per pair.
        static int ParityFor(int level) => level == 6 ? 2 : 1;

        // ===== Video =====
        var cameras = request.Cameras.Select(input =>
        {
            var perCamera = StorageMath.CameraTerabytes(input.BitrateMbps, days);
            return new StorageCameraLineDto(
                string.IsNullOrWhiteSpace(input.Label) ? "Cameras" : input.Label.Trim(),
                input.Count,
                input.BitrateMbps,
                StorageMath.RoundPrecise(perCamera),
                StorageMath.Round(perCamera * input.Count),
                StorageMath.Round(input.BitrateMbps * input.Count));
        }).ToList();

        var cameraCount = request.Cameras.Sum(x => x.Count);

        // Summed from unrounded figures, then rounded once: rounding each line
        // first and adding those would drift on a large job.
        // Motion scales the footage before redundancy: recording half the day
        // stores half as much. Clamped so a nonsense percentage cannot zero the
        // requirement and report an array of no disks.
        var motion = Math.Clamp(request.MotionPercent, 1, 100) / 100m;

        var videoRaw = request.Cameras.Sum(x =>
            StorageMath.CameraTerabytes(x.BitrateMbps, days) * x.Count) * motion;
        var video = videoRaw * request.Redundancy;
        var bandwidth = request.Cameras.Sum(x => x.BitrateMbps * x.Count);

        // ===== ANPR =====
        var usablePerDisk = request.Groups.Count > 0
            ? request.Groups[0].DiskTerabytes * request.FilesystemFactor
            : 0m;

        var anpr = request.Anpr.Select(input =>
        {
            var perCamera = StorageMath.SnapshotTerabytes(
                input.KilobytesPerImage, input.ImagesPerEvent, input.EventsPerDay, days);
            var total = perCamera * input.Count;

            return new StorageAnprLineDto(
                string.IsNullOrWhiteSpace(input.Label) ? "ANPR" : input.Label.Trim(),
                input.Count,
                StorageMath.RoundPrecise(perCamera),
                StorageMath.Round(total),
                StorageMath.DisksNeeded(total, usablePerDisk));
        }).ToList();

        var anprTotal = request.Anpr.Sum(x =>
            StorageMath.SnapshotTerabytes(x.KilobytesPerImage, x.ImagesPerEvent, x.EventsPerDay, days)
            * x.Count);

        // ===== Failover =====
        // Sized on the average camera across the design, since the covered
        // cameras are a count rather than a named subset.
        var averagePerCamera = cameraCount > 0 ? videoRaw / cameraCount : 0m;
        var failover = StorageMath.FailoverTerabytes(
            request.FailoverCameras, averagePerCamera, request.FailoverDays, days);

        // Split, not summed. Number-plate stills go on their own array: they are
        // written and read on completely different terms to continuous video,
        // and a site that fills its ANPR volume must not start eating into the
        // footage the retention period is promising. Combining them also hid
        // which half of a job actually needed the disks.
        var videoRequired = video + failover;
        var anprRequired = anprTotal;

        // Still reported as one figure, because the total capacity on site is a
        // real thing a submission has to state.
        var required = videoRequired + anprRequired;

        // Sized from the requirement rather than from anything the caller
        // proposed, so it answers "what do I buy" even when Groups is empty.
        // Weigh every stocked size and take the best, unless the caller pinned one.
        // The size is an outcome of the requirement, not something a person should
        // have to guess at before seeing the consequences.
        var comparison = StorageMath.CompareLevelsAndDisks(
            videoRequired,
            request.CandidateDisks.Select(c => new DiskCandidate(c.Terabytes, c.PricePerDisk)),
            request.FilesystemFactor,
            request.BaysPerGroup,
            request.HotSpareDisks,
            request.EnclosurePricePerGroup,
            levels);

        var best = comparison.FirstOrDefault(o => request.RecommendDiskTerabytes is null
                                                 || o.DiskTerabytes == request.RecommendDiskTerabytes)
                   ?? comparison.FirstOrDefault();

        var chosenDiskTb = request.RecommendDiskTerabytes ?? best?.DiskTerabytes ?? 16m;
        var chosenLevel = best?.RaidLevel ?? request.RaidLevel ?? 5;
        var parity = ParityFor(chosenLevel);

        // ===== Array =====
        var groups = request.Groups.Select((input, index) =>
        {
            var available = StorageMath.GroupTerabytes(
                input.TotalDisks, parity, input.DiskTerabytes, request.FilesystemFactor);

            return new StorageGroupDto(
                string.IsNullOrWhiteSpace(input.Label) ? $"GRP-{index + 1}" : input.Label.Trim(),
                input.TotalDisks,
                parity,
                input.TotalDisks - parity,
                input.DiskTerabytes,
                StorageMath.Round(available),
                StorageMath.CameraCeiling(available, averagePerCamera));
        }).ToList();

        var available = request.Groups.Sum(x =>
            StorageMath.GroupTerabytes(x.TotalDisks, parity, x.DiskTerabytes, request.FilesystemFactor));

        var coverage = required > 0 ? available / required * 100m : 0m;


        var recommended = StorageMath.RecommendArray(
            videoRequired,
            chosenDiskTb,
            request.FilesystemFactor,
            parity,
            request.BaysPerGroup,
            request.HotSpareDisks,
            chosenLevel);

        var diskOptions = comparison
            .Select(o => new StorageDiskOptionDto(
                o.DiskTerabytes,
                o.Array.TotalDisks,
                o.Array.Groups,
                o.Array.UsableTerabytes,
                o.Array.RawTerabytes,
                o.OverBuyTerabytes,
                o.DiskCost,
                o.EnclosureCost,
                o.TotalCost,
                o.DiskTerabytes == chosenDiskTb))
            .ToList();

        // ANPR picks its OWN disk, for the same reason the recording array does.
        // Chaining it to the recording size doubled the cost of a 2.35 TB volume
        // whenever the video happened to want 16 TB disks: 9,400 rather than
        // 4,600, for 28.8 TB of usable space against a 2.35 TB need. The two
        // arrays hold completely different amounts and there is no reason the
        // right disk for one is the right disk for the other.
        //
        // A pinned size applies to the RECORDING array only. Pinning is a choice
        // about the video disks; forcing it onto a 2.35 TB stills volume doubled
        // that array's cost for capacity it could not use. The two arrays are
        // sized separately everywhere else, and this is no different.
        var anprComparison = StorageMath.CompareLevelsAndDisks(
            anprRequired,
            request.CandidateDisks.Select(c => new DiskCandidate(c.Terabytes, c.PricePerDisk)),
            request.FilesystemFactor,
            request.BaysPerGroup,
            anprRequired > 0 ? request.HotSpareDisks : 0,
            request.EnclosurePricePerGroup,
            levels);

        var anprBest = anprComparison.FirstOrDefault(o => request.AnprDiskTerabytes is null
                                                         || o.DiskTerabytes == request.AnprDiskTerabytes)
                       ?? anprComparison.FirstOrDefault();

        var anprDiskTb = request.AnprDiskTerabytes ?? anprBest?.DiskTerabytes ?? chosenDiskTb;
        var anprLevel = anprBest?.RaidLevel ?? chosenLevel;
        var anprDiskOptionCost = anprComparison.FirstOrDefault()?.TotalCost;

        var recommendedAnpr = StorageMath.RecommendArray(
            anprRequired,
            anprDiskTb,
            request.FilesystemFactor,
            parity,
            request.BaysPerGroup,
            anprRequired > 0 ? request.HotSpareDisks : 0,
            anprLevel);

        // Days each array actually holds, which is not the days asked for: disks
        // are bought whole, so the last one always buys more than was needed.
        // A submission that states 120 days should be able to show the headroom.
        static int Achieved(decimal usable, decimal requiredTb, int days)
        {
            var perDay = days > 0 ? requiredTb / days : 0m;
            return perDay > 0 ? (int)Math.Floor(usable / perDay) : 0;
        }

        var achievedDays = Achieved(recommended.UsableTerabytes, videoRequired, days);
        var achievedAnprDays = Achieved(recommendedAnpr.UsableTerabytes, anprRequired, days);

        // What one array holding both would look like. Only reported when it is
        // genuinely cheaper — on a real job the separation is worth paying for,
        // and on a tiny one it is buying the 3-disk RAID minimum twice over.
        StorageSharedOptionDto? shared = null;
        if (anprRequired > 0 && recommendedAnpr.TotalDisks > 0)
        {
            var combined = StorageMath.CompareLevelsAndDisks(
                videoRequired + anprRequired,
                request.CandidateDisks.Select(c => new DiskCandidate(c.Terabytes, c.PricePerDisk)),
                request.FilesystemFactor, request.BaysPerGroup,
                request.HotSpareDisks, request.EnclosurePricePerGroup, levels)
                .FirstOrDefault();

            var separateDisks = recommended.TotalDisks + recommendedAnpr.TotalDisks;

            if (combined is not null && combined.Array.TotalDisks < separateDisks)
            {
                var separateCost = diskOptions.FirstOrDefault(o => o.Chosen)?.TotalCost;
                var anprCost = anprDiskOptionCost;

                shared = new StorageSharedOptionDto(
                    combined.Array.TotalDisks,
                    combined.Array.Groups,
                    combined.DiskTerabytes,
                    combined.Array.UsableTerabytes,
                    separateDisks - combined.Array.TotalDisks,
                    separateCost is not null && anprCost is not null && combined.TotalCost is not null
                        ? StorageMath.Round(separateCost.Value + anprCost.Value - combined.TotalCost.Value)
                        : null);
            }
        }

        return Task.FromResult(TypedResult<StorageDesignDto>.Success(new StorageDesignDto(
            days,
            request.Redundancy,
            request.FilesystemFactor,
            chosenLevel,

            request.RecordingCodec,
            request.Fps,
            Math.Clamp(request.MotionPercent, 1, 100),
            request.HddType,

            cameras,
            cameraCount,
            StorageMath.Round(videoRaw),
            StorageMath.Round(video),
            StorageMath.Round(bandwidth),

            anpr,
            StorageMath.Round(anprTotal),

            StorageMath.Round(failover),
            StorageMath.Round(required),

            new StorageRecommendationDto(
                chosenLevel,
                recommended.DataDisks,
                recommended.ParityDisks,
                recommended.HotSpareDisks,
                recommended.TotalDisks,
                recommended.Groups,
                recommended.DisksPerGroup,
                request.BaysPerGroup,
                chosenDiskTb,
                recommended.RawTerabytes,
                recommended.UsableTerabytes,
                StorageMath.Round(recommended.UsableTerabytes - videoRequired),
                achievedDays,
                recommended.Layout
                    .Select(g => new StorageGroupLayoutDto(
                        g.Number, g.DataDisks, g.ParityDisks, g.TotalDisks,
                        g.AvailableTerabytes, g.ProposedTerabytes))
                    .ToList()),

            new StorageRecommendationDto(
                // This array's own level, not the recording array's — the two are
                // weighed separately and routinely land on different ones.
                anprLevel,
                recommendedAnpr.DataDisks,
                recommendedAnpr.ParityDisks,
                recommendedAnpr.HotSpareDisks,
                recommendedAnpr.TotalDisks,
                recommendedAnpr.Groups,
                recommendedAnpr.DisksPerGroup,
                request.BaysPerGroup,
                anprDiskTb,
                recommendedAnpr.RawTerabytes,
                recommendedAnpr.UsableTerabytes,
                StorageMath.Round(recommendedAnpr.UsableTerabytes - anprRequired),
                achievedAnprDays,
                recommendedAnpr.Layout
                    .Select(g => new StorageGroupLayoutDto(
                        g.Number, g.DataDisks, g.ParityDisks, g.TotalDisks,
                        g.AvailableTerabytes, g.ProposedTerabytes))
                    .ToList()),

            StorageMath.Round(videoRequired),
            StorageMath.Round(anprRequired),

            diskOptions,
            anprComparison
                .Select(o => new StorageDiskOptionDto(
                    o.DiskTerabytes, o.Array.TotalDisks, o.Array.Groups,
                    o.Array.UsableTerabytes, o.Array.RawTerabytes, o.OverBuyTerabytes,
                    o.DiskCost, o.EnclosureCost, o.TotalCost,
                    o.DiskTerabytes == anprDiskTb))
                .ToList(),
            shared,

            groups,
            StorageMath.Round(available),
            StorageMath.Round(available - required),
            StorageMath.Round(coverage),
            available >= required && required > 0)));
    }
}
