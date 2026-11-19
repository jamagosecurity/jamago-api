using FluentValidation;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using Jama.Application.Maintenance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.MoiJobs.Services;

/// <summary>Adds a picture to a service on the MOI price list. Same rules
/// as a stock item's pictures: jpg/png/webp, 5 MB, checked against the file's
/// own bytes. Gated on maintenance.manage at the endpoint.</summary>
public sealed record UploadMoiServiceImageCommand : IRequest<ApiResult<MaintenanceImageDto>>
{
    public Guid ItemId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public Stream Content { get; init; } = Stream.Null;
}

public sealed class UploadMoiServiceImageCommandValidator : AbstractValidator<UploadMoiServiceImageCommand>
{
    private const long MaxBytes = 5L * 1024 * 1024;
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public UploadMoiServiceImageCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("A file name is required.")
            // Path.GetFileName strips any directory a client tried to send.
            .Must(name => !string.IsNullOrWhiteSpace(Path.GetFileName(name)))
            .WithMessage("A file name is required.")
            .MaximumLength(300).WithMessage("File name must be 300 characters or fewer.");

        RuleFor(x => x.FileName)
            .Must(name => AllowedExtensions.Contains(Path.GetExtension(Path.GetFileName(name)).ToLowerInvariant()))
            .WithMessage($"Accepted image types: {string.Join(", ", AllowedExtensions)}.")
            .When(x => !string.IsNullOrWhiteSpace(Path.GetFileName(x.FileName)));

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0).WithMessage("The image is empty.")
            .LessThanOrEqualTo(MaxBytes).WithMessage("Images must be 5 MB or smaller.");
    }
}

public sealed class UploadMoiServiceImageCommandHandler(
    IApplicationDbContext context,
    IFileStorage storage,
    TimeProvider timeProvider)
    : IRequestHandler<UploadMoiServiceImageCommand, ApiResult<MaintenanceImageDto>>
{
    private const int MaxImagesPerService = 8;

    public async Task<ApiResult<MaintenanceImageDto>> Handle(
        UploadMoiServiceImageCommand request, CancellationToken cancellationToken)
    {
        var item = await context.MoiServiceItems
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == request.ItemId, cancellationToken);

        if (item is null)
            return ApiResult<MaintenanceImageDto>.Failure("Service not found.");

        if (item.Images.Count >= MaxImagesPerService)
            return ApiResult<MaintenanceImageDto>.Failure($"A service can have at most {MaxImagesPerService} images.");

        var fileName = Path.GetFileName(request.FileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        // Read the header before writing anything: a file whose bytes do not
        // match the extension it claims never reaches storage.
        var header = new byte[FileSignatures.HeaderLength];
        var read = await request.Content.ReadAtLeastAsync(
            header, header.Length, throwOnEndOfStream: false, cancellationToken);

        if (!FileSignatures.Matches(extension, header.AsSpan(0, read)))
            return ApiResult<MaintenanceImageDto>.Failure($"That file is not a valid {extension} image.");

        if (!request.Content.CanSeek)
            return ApiResult<MaintenanceImageDto>.Failure("The upload could not be read. Please try again.");
        request.Content.Seek(0, SeekOrigin.Begin);

        var imageId = Guid.CreateVersion7();
        var storageKey = $"maintenance-services/{item.Id}/{imageId}{extension}";
        await storage.SaveAsync(request.Content, storageKey, cancellationToken);

        var image = new MoiServiceImage
        {
            Id = imageId,
            CatalogueItemId = item.Id,
            FileName = fileName,
            StorageKey = storageKey,
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes,
            // Appended to the end; the first upload becomes the main picture.
            SortOrder = item.Images.Count == 0 ? 0 : item.Images.Max(x => x.SortOrder) + 1,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        context.MoiServiceImages.Add(image);
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<MaintenanceImageDto>.Success(MoiServiceRules.ToDto(image));
    }
}
