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

    /// <summary>Video redundancy allowance. 1.06 in the workbook, where it was
    /// mislabelled "capacity loss due to formatting".</summary>
    public decimal Redundancy { get; init; } = 1.06m;

    /// <summary>Share of a disk left after formatting. 0.90.</summary>
    public decimal FilesystemFactor { get; init; } = 0.90m;

    /// <summary>Parity disks per group: 1 for RAID-5, 2 for RAID-6.</summary>
    public int RaidLevel { get; init; } = 5;

    /// <summary>Days of footage the failover copy keeps, and how many cameras it
    /// covers. Zero cameras means no failover block.</summary>
    public int FailoverDays { get; init; } = 30;
    public int FailoverCameras { get; init; }

    public IReadOnlyList<StorageCameraInput> Cameras { get; init; } = [];
    public IReadOnlyList<StorageAnprInput> Anpr { get; init; } = [];
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

    IReadOnlyList<StorageGroupDto> Groups,
    decimal AvailableTerabytes,
    /// <summary>Available minus required. Negative means the array is short.</summary>
    decimal SurplusTerabytes,
    /// <summary>Available as a percentage of required. 100 or more passes.</summary>
    decimal CoveragePercent,
    bool Covered);

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
            .Must(x => x is 5 or 6).WithMessage("RAID level must be 5 or 6.");

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
        var parity = request.RaidLevel == 6 ? 2 : 1;

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
        var videoRaw = request.Cameras.Sum(x =>
            StorageMath.CameraTerabytes(x.BitrateMbps, days) * x.Count);
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

        var required = video + anprTotal + failover;

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

        return Task.FromResult(TypedResult<StorageDesignDto>.Success(new StorageDesignDto(
            days,
            request.Redundancy,
            request.FilesystemFactor,
            request.RaidLevel,

            cameras,
            cameraCount,
            StorageMath.Round(videoRaw),
            StorageMath.Round(video),
            StorageMath.Round(bandwidth),

            anpr,
            StorageMath.Round(anprTotal),

            StorageMath.Round(failover),
            StorageMath.Round(required),

            groups,
            StorageMath.Round(available),
            StorageMath.Round(available - required),
            StorageMath.Round(coverage),
            available >= required && required > 0)));
    }
}
