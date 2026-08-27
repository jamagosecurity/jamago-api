using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Cameras.Commands.UploadCameraImage;

public sealed record UploadCameraImageCommand : IRequest<ApiResult<CameraImageDto>>
{
    public Guid CameraId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public Stream Content { get; init; } = Stream.Null;
}

public sealed class UploadCameraImageCommandHandler(
    IApplicationDbContext context,
    IFileStorage storage,
    TimeProvider timeProvider)
    : IRequestHandler<UploadCameraImageCommand, ApiResult<CameraImageDto>>
{
    /// <summary>Enough for a product gallery; beyond this it is an album, not a listing.</summary>
    private const int MaxImagesPerItem = 8;

    public async Task<ApiResult<CameraImageDto>> Handle(
        UploadCameraImageCommand request,
        CancellationToken cancellationToken)
    {
        var camera = await context.Cameras
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == request.CameraId, cancellationToken);

        if (camera is null)
            return ApiResult<CameraImageDto>.Failure("Camera not found.");

        if (camera.Images.Count >= MaxImagesPerItem)
            return ApiResult<CameraImageDto>.Failure($"An item can have at most {MaxImagesPerItem} images.");

        var fileName = Path.GetFileName(request.FileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        // Read the header before writing anything: a file whose bytes do not
        // match the extension it claims never reaches storage.
        var header = new byte[FileSignatures.HeaderLength];
        var read = await request.Content.ReadAtLeastAsync(
            header, header.Length, throwOnEndOfStream: false, cancellationToken);

        if (!FileSignatures.Matches(extension, header.AsSpan(0, read)))
            return ApiResult<CameraImageDto>.Failure($"That file is not a valid {extension} image.");

        // Rewind so the whole file — header included — gets written.
        if (request.Content.CanSeek)
            request.Content.Seek(0, SeekOrigin.Begin);
        else
            return ApiResult<CameraImageDto>.Failure("The upload could not be read. Please try again.");

        var imageId = Guid.CreateVersion7();
        var storageKey = $"cameras/{camera.Id}/{imageId}{extension}";
        await storage.SaveAsync(request.Content, storageKey, cancellationToken);

        var image = new CameraImage
        {
            Id = imageId,
            CameraId = camera.Id,
            FileName = fileName,
            StorageKey = storageKey,
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes,
            // Appended to the end of the gallery; the first upload becomes the
            // main picture and stays there.
            SortOrder = camera.Images.Count == 0 ? 0 : camera.Images.Max(x => x.SortOrder) + 1,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        context.CameraImages.Add(image);
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<CameraImageDto>.Success(CameraMappings.ToDto(image));
    }
}
