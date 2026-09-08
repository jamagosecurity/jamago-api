using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Drawings.Queries.GetDrawingFile;

/// <summary>Streamed content plus what the client needs to name and label it.
/// Mirrors VipDocumentContent.</summary>
public sealed record DrawingFileContent(Stream Content, string FileName, string ContentType);

public sealed record GetDrawingFileQuery(Guid FileId) : IRequest<ApiResult<DrawingFileContent>>;

public sealed class GetDrawingFileQueryHandler(
    IApplicationDbContext context,
    IFileStorage storage,
    IPdfWatermarker watermarker,
    ICurrentUser actor)
    : IRequestHandler<GetDrawingFileQuery, ApiResult<DrawingFileContent>>
{
    public async Task<ApiResult<DrawingFileContent>> Handle(
        GetDrawingFileQuery request,
        CancellationToken cancellationToken)
    {
        var file = await context.DrawingFiles
            .AsNoTracking()
            .Include(x => x.Drawing)
            .FirstOrDefaultAsync(x => x.Id == request.FileId, cancellationToken);

        if (file is null || !DrawingVisibility.CanSee(file.Drawing.Status, file.Drawing.PreparedById, actor))
            return ApiResult<DrawingFileContent>.Failure("File not found.");

        if (!DrawingVisibility.CanDownload(file.Drawing.Status, actor))
            return ApiResult<DrawingFileContent>.Failure(
                "This drawing has not been approved yet. Only an approver can preview it before then.");

        var isPdf = Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
        var needsWatermark = DrawingVisibility.Watermark(file.Drawing.Status);

        // A native CAD file (DWG/DXF/ZIP) cannot be stamped, so it stays fully
        // blocked pre-approval — even for an approver, who reviews the plotted
        // PDF to decide, not the raw drawing file. No half-measure preview.
        if (needsWatermark && !isPdf)
            return ApiResult<DrawingFileContent>.Failure(
                "Only the plotted PDF can be previewed before approval. The original file becomes available once approved.");

        if (needsWatermark && isPdf)
        {
            await using var pdfStream = await storage.OpenReadAsync(file.StorageKey, cancellationToken);
            if (pdfStream is null)
                return ApiResult<DrawingFileContent>.Failure("The stored file is missing.");

            using var buffer = new MemoryStream();
            await pdfStream.CopyToAsync(buffer, cancellationToken);

            byte[] stamped;
            try
            {
                stamped = watermarker.Stamp(buffer.ToArray());
            }
            catch (Exception)
            {
                // The upload passed FileSignatures' magic-byte check, but that
                // only confirms the header — a file that is otherwise malformed
                // can still fail here. Untrusted input feeding a PDF parser
                // must never surface as an unhandled 500; refuse cleanly
                // instead, same as a missing stored file just below.
                return ApiResult<DrawingFileContent>.Failure(
                    "This file could not be opened for preview. Contact an administrator.");
            }

            return ApiResult<DrawingFileContent>.Success(
                new DrawingFileContent(new MemoryStream(stamped), file.FileName, file.ContentType));
        }

        var content = await storage.OpenReadAsync(file.StorageKey, cancellationToken);
        if (content is null)
            return ApiResult<DrawingFileContent>.Failure("The stored file is missing.");

        return ApiResult<DrawingFileContent>.Success(
            new DrawingFileContent(content, file.FileName, file.ContentType));
    }
}
