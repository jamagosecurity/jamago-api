using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Drawings.Commands.UploadDrawingFile;

public sealed record UploadDrawingFileCommand : IRequest<ApiResult<DrawingFileDto>>
{
    public Guid DrawingId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public long SizeBytes { get; init; }
    public Stream Content { get; init; } = Stream.Null;
}

/// <summary>
/// Adds one file to a drawing — the DWG, the plotted PDF, or a ZIP of the
/// drawing with its xrefs. Mirrors UploadVipDocumentCommandHandler: the header
/// bytes are checked against the extension the name claims, and the storage key
/// is built entirely from server-side ids so the uploaded name never reaches the
/// filesystem.
/// </summary>
public sealed class UploadDrawingFileCommandHandler(
    IApplicationDbContext context,
    IFileStorage storage,
    ICurrentUser actor,
    TimeProvider timeProvider)
    : IRequestHandler<UploadDrawingFileCommand, ApiResult<DrawingFileDto>>
{
    public async Task<ApiResult<DrawingFileDto>> Handle(
        UploadDrawingFileCommand request,
        CancellationToken cancellationToken)
    {
        var drawing = await context.Drawings
            .FirstOrDefaultAsync(x => x.Id == request.DrawingId, cancellationToken);

        if (drawing is null)
            return ApiResult<DrawingFileDto>.Failure("Drawing not found.");

        // Files are content, on the same terms as a quotation's lines: adding
        // one to a document already sitting with an approver, or already
        // approved, is an edit and follows the same rule as any other.
        var amending = !DrawingWorkflow.IsEditable(drawing.Status);
        if (amending && !actor.IsSuperAdmin)
            return ApiResult<DrawingFileDto>.Failure(
                "An approved drawing can only be changed by the super administrator.");

        var fileName = Path.GetFileName(request.FileName).Trim();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        // The validator already checked the extension against the drawing
        // allow-list and the size against DrawingMaxFileSizeMb. What is left
        // here needs the database — the drawing must exist — and the content
        // itself, which the validator never sees.
        var header = new byte[FileSignatures.HeaderLength];
        var headerLength = await ReadHeaderAsync(request.Content, header, cancellationToken);

        if (!FileSignatures.Matches(extension, header.AsSpan(0, headerLength)))
        {
            return ApiResult<DrawingFileDto>.Failure(
                $"This file is not a valid {extension.TrimStart('.').ToUpperInvariant()} file. "
                + "Check the file opens correctly and try again.");
        }

        var content = request.Content.CanSeek
            ? Rewind(request.Content)
            : new PrefixedStream(header.AsMemory(0, headerLength), request.Content);

        // Joined with '/' rather than Path.Combine — see UploadVipDocumentCommandHandler
        // for why: keys are persisted and shared between Windows development and
        // the Linux VPS.
        var fileId = Guid.CreateVersion7();
        var storageKey = string.Join('/', "drawings", drawing.Id.ToString(), $"{fileId}{extension}");

        await storage.SaveAsync(content, storageKey, cancellationToken);

        var file = new DrawingFile
        {
            Id = fileId,
            DrawingId = drawing.Id,
            FileName = fileName,
            StorageKey = storageKey,
            ContentType = string.IsNullOrWhiteSpace(request.ContentType)
                ? "application/octet-stream"
                : request.ContentType,
            SizeBytes = request.SizeBytes,
            UploadedById = actor.UserId,
            UploadedByName = actor.DisplayName,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        context.DrawingFiles.Add(file);

        if (amending)
            DrawingWorkflow.Record(context, drawing, DrawingApprovalAction.Amended, actor, file.CreatedAt);

        drawing.UpdatedAt = file.CreatedAt;
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<DrawingFileDto>.Success(DrawingMappings.ToFileDto(file));
    }

    private static async Task<int> ReadHeaderAsync(
        Stream content, byte[] header, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < header.Length)
        {
            var read = await content.ReadAsync(header.AsMemory(total), cancellationToken);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    private static Stream Rewind(Stream content)
    {
        content.Seek(0, SeekOrigin.Begin);
        return content;
    }

    /// <summary>Replays a prefix already read from an unseekable stream, then
    /// continues with the rest of it. Mirrors the private type in
    /// UploadVipDocumentCommandHandler.</summary>
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
            Memory<byte> buffer, CancellationToken cancellationToken = default)
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
