using System.Globalization;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Application.StorageDesigns.Queries.CalculateStorageDesign;
using MediatR;

namespace Jama.Application.StorageDesigns.Queries.GetStorageDesignPdf;

/// <summary>
/// The MOI storage sheet as a PDF.
///
/// Carries the same inputs as the calculation plus the few things only the
/// document needs — the project it is for, the revision, and the recorder the
/// quotation prices. The figures are not passed in: this runs the calculation
/// itself, so a downloaded sheet cannot disagree with the screen that offered it.
/// </summary>
public sealed record GetStorageDesignPdfQuery : IRequest<TypedResult<StorageDesignPdfDto>>
{
    public CalculateStorageDesignQuery Design { get; init; } = new();

    public string? ProjectName { get; init; }
    public string RevisionNo { get; init; } = "REV.00";

    /// <summary>The recorder from the quotation. Blank when it prices none, which
    /// the sheet states rather than inventing a model.</summary>
    public string? RecorderLabel { get; init; }
    public int RecorderChannels { get; init; }
}

public sealed record StorageDesignPdfDto(string FileName, byte[] Content);

public sealed class GetStorageDesignPdfQueryHandler(
    ISender sender,
    IMoiStoragePdfGenerator generator,
    TimeProvider timeProvider)
    : IRequestHandler<GetStorageDesignPdfQuery, TypedResult<StorageDesignPdfDto>>
{
    public async Task<TypedResult<StorageDesignPdfDto>> Handle(
        GetStorageDesignPdfQuery request,
        CancellationToken cancellationToken)
    {
        var calculated = await sender.Send(request.Design, cancellationToken);
        if (!calculated.Succeeded || calculated.Data is null)
            return TypedResult<StorageDesignPdfDto>.Failure(calculated.Errors);

        var design = calculated.Data;
        var project = string.IsNullOrWhiteSpace(request.ProjectName) ? "—" : request.ProjectName.Trim();
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var recorder = string.IsNullOrWhiteSpace(request.RecorderLabel)
            ? "Not on the quotation"
            : request.RecorderLabel.Trim();

        var primary = Sheet(
            design,
            design.Recommended,
            "CCTV SYSTEM STORAGE CALCULATION",
            $"PRIMARY STORAGE: {design.RetentionDays} DAYS",
            project,
            recorder,
            request.RecorderChannels,
            design.CameraCount,
            Bitrate(design),
            PerCamera(design.VideoTerabytes, design.CameraCount),
            design.VideoRequiredTerabytes);

        MoiStorageSheet? anpr = null;
        MoiSnapshotBlock? snapshot = null;

        if (design.RecommendedAnpr.TotalDisks > 0)
        {
            var anprCameras = design.Anpr.Sum(x => x.Count);

            anpr = Sheet(
                design,
                design.RecommendedAnpr,
                "ANPR SYSTEM STORAGE CALCULATION",
                $"ANPR STORAGE: {design.RetentionDays} DAYS",
                project,
                recorder,
                request.RecorderChannels,
                anprCameras,
                Bitrate(design),
                PerCamera(design.AnprRequiredTerabytes, anprCameras),
                design.AnprRequiredTerabytes);

            var first = design.Anpr.FirstOrDefault();
            snapshot = new MoiSnapshotBlock(
                // Defaults mirror the ones the calculation used; the request shape
                // does not carry them back on the response.
                SnapshotsPerDay: 7_000,
                SnapshotKilobytes: 500,
                RecordingDays: design.RetentionDays,
                PerCameraTerabytes: first?.PerCameraTerabytes ?? 0m,
                Cameras: anprCameras,
                DiskTerabytes: design.RecommendedAnpr.DiskTerabytes,
                RequiredTerabytes: design.AnprRequiredTerabytes);
        }

        var model = new MoiStoragePdfModel(project, request.RevisionNo, today, primary, anpr, snapshot);

        var safe = string.Join("_", project.Split(Path.GetInvalidFileNameChars()));
        var name = $"Storage Calculation - {safe}.pdf";

        return TypedResult<StorageDesignPdfDto>.Success(
            new StorageDesignPdfDto(name, generator.Generate(model)));
    }

    /// <summary>Weighted by camera count, so one outlier does not read as the
    /// midpoint of two numbers.</summary>
    private static decimal Bitrate(StorageDesignDto design)
    {
        var total = design.Cameras.Sum(x => x.Count);
        if (total == 0) return 0m;
        return design.Cameras.Sum(x => x.BitrateMbps * x.Count) / total;
    }

    private static decimal PerCamera(decimal total, int cameras) =>
        cameras > 0 ? Math.Round(total / cameras, 4, MidpointRounding.AwayFromZero) : 0m;

    private static MoiStorageSheet Sheet(
        StorageDesignDto design,
        StorageRecommendationDto array,
        string title,
        string subtitle,
        string project,
        string recorder,
        int channels,
        int cameras,
        decimal bitrate,
        decimal perCamera,
        decimal required) =>
        new(
            title,
            subtitle,
            project,
            recorder,
            channels,
            array.BaysPerGroup,
            design.HddType,
            array.DiskTerabytes,
            array.Layout
                .Select(g => new MoiArrayRow(
                    ((char)('A' + g.Number - 1)).ToString(),
                    g.DataDisks,
                    g.ParityDisks,
                    g.AvailableTerabytes,
                    g.ProposedTerabytes))
                .ToList(),
            array.HotSpareDisks,
            array.TotalDisks,
            cameras,
            // One resolution when every camera agrees, otherwise "Mixed" — the
            // sheet must not assert a single figure over a mixed system.
            design.Cameras.Select(x => x.BitrateMbps).Distinct().Count() == 1 ? "2MP" : "Mixed",
            design.RecordingCodec,
            design.Fps,
            bitrate,
            design.MotionPercent,
            design.RetentionDays,
            perCamera,
            required,
            array.UsableTerabytes,
            array.DataDisks * array.DiskTerabytes,
            array.RawTerabytes,
            array.RaidLevel,
            design.Redundancy);
}
