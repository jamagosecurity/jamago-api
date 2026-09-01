using System.Globalization;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Boqs.Queries.GetBoqPdf;

public sealed record BoqPdfDto(byte[] Content, string FileName);

public sealed record GetBoqPdfQuery(Guid Id) : IRequest<ApiResult<BoqPdfDto>>;

public sealed class GetBoqPdfQueryHandler(
    IApplicationDbContext context,
    IBoqPdfGenerator generator,
    IFileStorage storage)
    : IRequestHandler<GetBoqPdfQuery, ApiResult<BoqPdfDto>>
{
    public async Task<ApiResult<BoqPdfDto>> Handle(
        GetBoqPdfQuery request,
        CancellationToken cancellationToken)
    {
        var boq = await context.Boqs
            .AsNoTracking()
            .Include(x => x.Sections)
            .ThenInclude(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (boq is null)
            return ApiResult<BoqPdfDto>.Failure("BOQ not found.");

        // Description and photo live on the stock item, not on the line — a line
        // copies price and name so an approved quotation cannot be rewritten by
        // an inventory edit, but the picture is just how the item looks today.
        //
        // Fetched once for every item on the document rather than per line: the
        // same camera appears in several sections on a real job.
        var itemIds = boq.Sections
            .SelectMany(s => s.Lines)
            .Where(l => l.CameraId.HasValue)
            .Select(l => l.CameraId!.Value)
            .Distinct()
            .ToList();

        var items = await context.Cameras
            .AsNoTracking()
            .Where(x => itemIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.DescriptionEn,
                x.DescriptionAr,
                // First by sort order — the one the catalogue shows as primary.
                StorageKey = x.Images.OrderBy(i => i.SortOrder)
                    .Select(i => i.StorageKey)
                    .FirstOrDefault(),
            })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var photos = new Dictionary<Guid, byte[]?>();
        foreach (var item in items.Values)
        {
            if (string.IsNullOrWhiteSpace(item.StorageKey)) continue;

            // A missing file must not fail the whole document: the row prints
            // without a picture, which is what the catalogue screen does too.
            await using var stream = await storage.OpenReadAsync(item.StorageKey, cancellationToken);
            if (stream is null) continue;

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            photos[item.Id] = buffer.ToArray();
        }

        var sections = boq.Sections
            .OrderBy(s => s.SortOrder)
            .Select((section, sectionIndex) =>
            {
                var number = (sectionIndex + 1).ToString(CultureInfo.InvariantCulture);
                var lines = section.Lines
                    .OrderBy(l => l.SortOrder)
                    .Select((line, lineIndex) => new BoqPdfLine(
                        string.Create(CultureInfo.InvariantCulture, $"{number}.{lineIndex + 1}"),
                        line.ItemName,
                        line.ModelNo,
                        line.Brand,
                        line.Type,
                        line.Uom.ToString(),
                        line.Quantity,
                        line.UnitRate,
                        line.LineTotal)
                    {
                        Description = line.CameraId is { } id && items.TryGetValue(id, out var item)
                            ? item.DescriptionEn
                            : null,
                        DescriptionAr = line.CameraId is { } arId && items.TryGetValue(arId, out var arItem)
                            ? arItem.DescriptionAr
                            : null,
                        Image = line.CameraId is { } photoId && photos.TryGetValue(photoId, out var bytes)
                            ? bytes
                            : null,
                    })
                    .ToList();

                return new BoqPdfSection(number, section.Title, lines.Sum(l => l.LineTotal), lines);
            })
            .ToList();

        var model = new BoqPdfModel(
            boq.BoqNumber,
            boq.ProjectName,
            boq.SiteLocation,
            boq.ClientName,
            boq.ContactNumber,
            boq.IssueDate,
            boq.Status.ToString(),
            boq.Notes,
            boq.PreparedByName,
            boq.Total,
            sections);

        return ApiResult<BoqPdfDto>.Success(
            new BoqPdfDto(generator.Generate(model), $"{boq.BoqNumber}.pdf"));
    }
}
