using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;

namespace Jama.Application.Proformas.Queries.GetProformaFile;

public sealed record ProformaFileDto(byte[] Content, string FileName, string ContentType);

/// <summary>
/// The proforma invoice of a quotation as a PDF, Excel or Word file — the same document in each. An approved quotation
/// prints its kept PI number, date, P.O. and customer details (<see cref="Commands.SaveProforma.SaveProformaCommand"/>
/// makes them first); any other prints as a DRAFT with no number. Lines, prices and totals are the quotation's own.
///
/// <paramref name="Typed"/> is a preview: the document with the values typed on screen. Nothing is saved and no number
/// is used up; an approved quotation that has no PI number yet says the number is given on download.
/// </summary>
public sealed record GetProformaFileQuery(Guid BoqId, ExportFormat Format = ExportFormat.Pdf, ProformaInput? Typed = null)
    : IRequest<ApiResult<ProformaFileDto>>;

public sealed class GetProformaFileQueryHandler(
    IApplicationDbContext context,
    IProformaPdfGenerator pdf,
    IProformaExcelGenerator excel,
    IProformaWordGenerator word,
    TimeProvider clock)
    : IRequestHandler<GetProformaFileQuery, ApiResult<ProformaFileDto>>
{
    public async Task<ApiResult<ProformaFileDto>> Handle(GetProformaFileQuery r, CancellationToken ct)
    {
        var source = await ProformaDocument.LoadAsync(context, r.BoqId, ct);
        if (source is null) return ApiResult<ProformaFileDto>.Failure("Quotation not found.");
        if (!source.Draft && source.Kept is null && r.Typed is null)
            return ApiResult<ProformaFileDto>.Failure("Make the proforma invoice first.");

        var model = ProformaDocument.Build(source, clock, r.Typed);
        var name = source.Kept?.Number
            ?? (source.Draft ? $"Proforma-DRAFT-{source.Boq.BoqNumber}" : $"Proforma-PREVIEW-{source.Boq.BoqNumber}");

        var content = r.Format switch
        {
            ExportFormat.Excel => excel.Generate(model),
            ExportFormat.Word => word.Generate(model),
            _ => pdf.Generate(model),
        };
        return ApiResult<ProformaFileDto>.Success(
            new ProformaFileDto(content, $"{name}.{r.Format.Extension()}", r.Format.ContentType()));
    }
}
