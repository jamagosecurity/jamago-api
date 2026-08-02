using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Application.Options;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jama.Application.VipClients.Commands.UploadVipDocument;

public sealed record UploadVipDocumentCommand : IRequest<ApiResult<VipClientDocumentDto>>
{
    public Guid FolderId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public long SizeBytes { get; init; }
    public Stream Content { get; init; } = Stream.Null;
}

public sealed class UploadVipDocumentCommandHandler(
    IApplicationDbContext context,
    IFileStorage storage,
    ICurrentUser currentUser,
    IOptions<FileStorageSettings> options)
    : IRequestHandler<UploadVipDocumentCommand, ApiResult<VipClientDocumentDto>>
{
    public async Task<ApiResult<VipClientDocumentDto>> Handle(
        UploadVipDocumentCommand request,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        var folder = await context.VipClientFolders
            .Include(f => f.VipClient)
            .FirstOrDefaultAsync(f => f.Id == request.FolderId, cancellationToken);

        if (folder is null)
            return ApiResult<VipClientDocumentDto>.Failure("Folder not found.");

        var fileName = Path.GetFileName(request.FileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fileName))
            return ApiResult<VipClientDocumentDto>.Failure("A file name is required.");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!settings.AllowedExtensions.Contains(extension))
        {
            return ApiResult<VipClientDocumentDto>.Failure(
                $"{extension} files are not allowed. Accepted types: {string.Join(", ", settings.AllowedExtensions)}.");
        }

        var maxBytes = settings.MaxFileSizeMb * 1024L * 1024L;
        if (request.SizeBytes > maxBytes)
            return ApiResult<VipClientDocumentDto>.Failure($"Files must be {settings.MaxFileSizeMb} MB or smaller.");

        // Key is built entirely from server-side ids — the uploaded name never
        // reaches the filesystem, so it cannot escape the storage root or
        // collide with another client's file.
        //
        // Joined with '/' rather than Path.Combine on purpose. Keys are persisted
        // and this database is shared between Windows development and the Linux
        // VPS: "vip\a\b\c.pdf" is a single flat filename on Linux, so the file
        // would be unreachable from production.
        var documentId = Guid.CreateVersion7();
        var storageKey = string.Join('/',
            "vip",
            folder.VipClientId.ToString(),
            folder.Id.ToString(),
            $"{documentId}{extension}");

        await storage.SaveAsync(request.Content, storageKey, cancellationToken);

        var document = new VipClientDocument
        {
            Id = documentId,
            VipClientFolderId = folder.Id,
            FileName = fileName,
            StorageKey = storageKey,
            ContentType = string.IsNullOrWhiteSpace(request.ContentType)
                ? "application/octet-stream"
                : request.ContentType,
            SizeBytes = request.SizeBytes,
            UploadedById = currentUser.UserId,
        };

        context.VipClientDocuments.Add(document);
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<VipClientDocumentDto>.Success(new VipClientDocumentDto(
            document.Id,
            document.FileName,
            document.ContentType,
            document.SizeBytes,
            document.CreatedAt,
            currentUser.DisplayName));
    }
}
