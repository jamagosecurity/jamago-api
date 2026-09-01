using Jama.Application.StorageDesigns.Queries.CalculateStorageDesign;

namespace Jama.Application.Common.Interfaces;

/// <summary>One RAID set as the sheet lists it: a lettered pool and its disks.</summary>
public sealed record MoiArrayRow(
    string Letter,
    int DataDisks,
    int ParityDisks,
    decimal AvailableTerabytes,
    decimal ProposedTerabytes);

/// <summary>
/// One of the sheet's two storage tables — primary CCTV, or ANPR.
///
/// Both pages carry identical columns, so they are the same shape here and the
/// generator draws them with one routine rather than two that drift apart.
/// </summary>
public sealed record MoiStorageSheet(
    string Title,
    string Subtitle,
    string ProjectName,
    string RecorderLabel,
    int Channels,
    int HddBays,
    string HddType,
    decimal DiskTerabytes,
    IReadOnlyList<MoiArrayRow> Arrays,
    int HotSpareDisks,
    int TotalDisks,
    int Cameras,
    string Resolution,
    string Codec,
    int Fps,
    decimal BitrateMbps,
    int MotionPercent,
    int RecordingDays,
    decimal PerCameraTerabytes,
    decimal RequiredTerabytes,
    decimal AvailableTerabytes,
    decimal ProposedTerabytes,
    decimal ProposedIncludingRaidTerabytes,
    int RaidLevel,
    decimal Redundancy);

/// <summary>The ANPR snapshot block, which is sized from stills rather than video.</summary>
public sealed record MoiSnapshotBlock(
    int SnapshotsPerDay,
    int SnapshotKilobytes,
    int RecordingDays,
    decimal PerCameraTerabytes,
    int Cameras,
    decimal DiskTerabytes,
    decimal RequiredTerabytes);

/// <summary>
/// Everything the MOI storage sheet states, already reduced to the figures that
/// appear on it. The generator lays this out; it works nothing out itself, so the
/// document and the calculator can never disagree.
/// </summary>
public sealed record MoiStoragePdfModel(
    string ProjectName,
    string RevisionNo,
    DateOnly Date,
    MoiStorageSheet Primary,
    MoiStorageSheet? Anpr,
    MoiSnapshotBlock? Snapshot);

public interface IMoiStoragePdfGenerator
{
    byte[] Generate(MoiStoragePdfModel model);
}
