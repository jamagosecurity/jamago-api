using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
    ICurrentUser currentUser)
    : IRequestHandler<UploadVipDocumentCommand, ApiResult<VipClientDocumentDto>>
{
    public async Task<ApiResult<VipClientDocumentDto>> Handle(
        UploadVipDocumentCommand request,
        CancellationToken cancellationToken)
    {
        // File name, extension and size are checked by
        // UploadVipDocumentCommandValidator before this runs. What is left here
        // is the part that needs the database: the folder must exist.
        var folder = await context.VipClientFolders
            .Include(f => f.VipClient)
            .FirstOrDefaultAsync(f => f.Id == request.FolderId, cancellationToken);

        if (folder is null)
            return ApiResult<VipClientDocumentDto>.Failure("Folder not found.");

        var fileName = Path.GetFileName(request.FileName).Trim();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        // The validator trusted the extension, which is only a string on the end
        // of a name. Read the real leading bytes so a file that was never the
        // declared type is refused before it lands beside genuine documents.
        var header = new byte[FileSignatures.HeaderLength];
        var headerLength = await ReadHeaderAsync(request.Content, header, cancellationToken);

        if (!FileSignatures.Matches(extension, header.AsSpan(0, headerLength)))
        {
            return ApiResult<VipClientDocumentDto>.Failure(
                $"This file is not a valid {extension.TrimStart('.').ToUpperInvariant()} file. "
                + "Check the file opens correctly and try again.");
        }

        // Reading the header consumed it, so put it back before the content is
        // written — by rewinding where possible, and otherwise by stitching the
        // bytes we took back onto the front of the stream.
        var content = request.Content.CanSeek
            ? Rewind(request.Content)
            : new PrefixedStream(header.AsMemory(0, headerLength), request.Content);

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

        await storage.SaveAsync(content, storageKey, cancellationToken);

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

    /// <summary>
    /// Fills as much of <paramref name="header"/> as the stream has. A short read
    /// is not an error — a file can legitimately be smaller than the buffer — so
    /// this returns how many bytes were actually available.
    /// </summary>
    private static async Task<int> ReadHeaderAsync(
        Stream content,
        byte[] header,
        CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < header.Length)
        {
            var read = await content.ReadAsync(header.AsMemory(total), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private static Stream Rewind(Stream content)
    {
        content.Seek(0, SeekOrigin.Begin);
        return content;
    }

    /// <summary>
    /// Replays a prefix already read from an unseekable stream, then continues
    /// with the rest of it. Only the reads the file writer performs are
    /// supported, which is why everything else throws rather than pretending.
    /// </summary>
    private sealed class PrefixedStream(ReadOnlyMemory<byte> prefix, Stream rest) : Stream
    {
        private int _prefixPosition;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_prefixPosition < prefix.Length)
            {
                var take = Math.Min(buffer.Length, prefix.Length - _prefixPosition);
                prefix.Slice(_prefixPosition, take).CopyTo(buffer);
                _prefixPosition += take;
                return take;
            }

            return await rest.ReadAsync(buffer, cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
