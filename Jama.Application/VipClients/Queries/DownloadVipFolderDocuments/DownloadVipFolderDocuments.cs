using System.IO.Compression;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.VipClients.Queries.DownloadVipFolderDocuments;

/// <summary>
/// Zips every document in a folder, or — when <see cref="DocumentIds"/> is
/// given — only the ones picked. Same authorization shape as a single
/// document download: a client may only reach their own project.
/// </summary>
public sealed record DownloadVipFolderDocumentsQuery(Guid FolderId, IReadOnlyList<Guid>? DocumentIds)
    : IRequest<ApiResult<VipDocumentContent>>;

public sealed class DownloadVipFolderDocumentsQueryHandler(
    IApplicationDbContext context,
    IFileStorage storage,
    ICurrentUser currentUser)
    : IRequestHandler<DownloadVipFolderDocumentsQuery, ApiResult<VipDocumentContent>>
{
    public async Task<ApiResult<VipDocumentContent>> Handle(
        DownloadVipFolderDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        var folder = await context.VipClientFolders
            .Include(f => f.VipClient)
            .Include(f => f.Documents)
            .FirstOrDefaultAsync(f => f.Id == request.FolderId, cancellationToken);

        if (folder is null)
            return ApiResult<VipDocumentContent>.Failure("Folder not found.");

        // Same rule as a single document download: confirming the folder
        // exists for another client would leak that it holds files.
        if (currentUser.Role == Roles.Client && folder.VipClient.AdminUserId != currentUser.UserId)
            return ApiResult<VipDocumentContent>.Failure("Folder not found.");

        var documents = request.DocumentIds is { Count: > 0 }
            ? folder.Documents.Where(d => request.DocumentIds.Contains(d.Id)).ToList()
            : folder.Documents.ToList();

        if (documents.Count == 0)
            return ApiResult<VipDocumentContent>.Failure("No files to download.");

        var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Zip entry names must be unique; two documents can legitimately
            // share a FileName (uploaded separately, or re-uploaded after a
            // delete), so collisions get a " (2)", " (3)"... suffix.
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var document in documents)
            {
                var content = await storage.OpenReadAsync(document.StorageKey, cancellationToken);
                if (content is null)
                    continue; // Missing on disk — skip it rather than fail the whole zip.

                var entry = archive.CreateEntry(UniqueName(document.FileName, usedNames), CompressionLevel.NoCompression);
                await using var entryStream = entry.Open();

                // Already-compressed or not, the archive holds the original bytes —
                // no point gzip-compressing a second time inside the zip entry
                // (hence CompressionLevel.NoCompression above), but a gzip-stored
                // document still needs unwrapping first.
                if (document.IsCompressed)
                {
                    await using var gzip = new GZipStream(content, CompressionMode.Decompress);
                    await gzip.CopyToAsync(entryStream, cancellationToken);
                }
                else
                {
                    await content.CopyToAsync(entryStream, cancellationToken);
                    await content.DisposeAsync();
                }
            }
        }

        zipStream.Seek(0, SeekOrigin.Begin);

        var zipName = request.DocumentIds is { Count: > 0 } && documents.Count == 1
            ? Path.ChangeExtension(documents[0].FileName, ".zip")
            : $"{folder.Name}.zip";

        return ApiResult<VipDocumentContent>.Success(new VipDocumentContent(zipStream, zipName, "application/zip"));
    }

    private static string UniqueName(string fileName, HashSet<string> usedNames)
    {
        var candidate = fileName;
        var attempt = 1;

        while (!usedNames.Add(candidate))
        {
            attempt++;
            var extension = Path.GetExtension(fileName);
            var stem = Path.GetFileNameWithoutExtension(fileName);
            candidate = $"{stem} ({attempt}){extension}";
        }

        return candidate;
    }
}
