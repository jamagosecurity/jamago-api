using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Application.Options;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jama.Application.VipClients;

public sealed record UploadVipDocumentCommand : IRequest<ApiResult<VipClientDocumentDto>>
{
    public Guid FolderId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public long SizeBytes { get; init; }
    public Stream Content { get; init; } = Stream.Null;
}

public sealed record DeleteVipDocumentCommand(Guid DocumentId) : IRequest<ApiResult<Guid>>;

/// <summary>
/// Resolved content for a download. Access is decided in the handler, not the
/// endpoint, so both the admin and the client route enforce the same rule.
/// </summary>
public sealed record VipDocumentContent(Stream Content, string FileName, string ContentType);

public sealed record DownloadVipDocumentQuery(Guid DocumentId)
    : IRequest<ApiResult<VipDocumentContent>>;

public sealed class UploadVipDocumentHandler(
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
        // reaches the filesystem, so it cannot be used to escape the root or
        // collide with another client's file.
        //
        // Joined with '/' rather than Path.Combine on purpose. Keys are persisted
        // and this database is shared between Windows development and the Linux
        // VPS: a key written as "vip\a\b\c.pdf" on Windows is a single flat
        // filename on Linux, so the file would be unreachable from production.
        // The storage implementation translates to the local separator.
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

public sealed class DownloadVipDocumentHandler(
    IApplicationDbContext context,
    IFileStorage storage,
    ICurrentUser currentUser)
    : IRequestHandler<DownloadVipDocumentQuery, ApiResult<VipDocumentContent>>
{
    public async Task<ApiResult<VipDocumentContent>> Handle(
        DownloadVipDocumentQuery request,
        CancellationToken cancellationToken)
    {
        var document = await context.VipClientDocuments
            .AsNoTracking()
            .Include(d => d.Folder)
                .ThenInclude(f => f.VipClient)
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken);

        if (document is null)
            return ApiResult<VipDocumentContent>.Failure("Document not found.");

        // A client may only read their own project. Staff with vip.manage reach
        // this through a permission-gated route, so the role check here is what
        // stops one client guessing another's document id.
        if (currentUser.Role == Roles.Client
            && document.Folder.VipClient.AdminUserId != currentUser.UserId)
        {
            return ApiResult<VipDocumentContent>.Failure("Document not found.");
        }

        var content = await storage.OpenReadAsync(document.StorageKey, cancellationToken);
        if (content is null)
            return ApiResult<VipDocumentContent>.Failure("The stored file is missing.");

        return ApiResult<VipDocumentContent>.Success(
            new VipDocumentContent(content, document.FileName, document.ContentType));
    }
}

public sealed class DeleteVipDocumentHandler(IApplicationDbContext context, IFileStorage storage)
    : IRequestHandler<DeleteVipDocumentCommand, ApiResult<Guid>>
{
    public async Task<ApiResult<Guid>> Handle(
        DeleteVipDocumentCommand request,
        CancellationToken cancellationToken)
    {
        var document = await context.VipClientDocuments
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken);

        if (document is null)
            return ApiResult<Guid>.Failure("Document not found.");

        var key = document.StorageKey;
        context.VipClientDocuments.Remove(document);
        await context.SaveChangesAsync(cancellationToken);

        await storage.DeleteAsync(key, cancellationToken);
        return ApiResult<Guid>.Success(document.Id);
    }
}
