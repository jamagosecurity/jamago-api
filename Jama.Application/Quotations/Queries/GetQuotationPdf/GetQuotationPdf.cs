using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Quotations.Queries.GetQuotationPdf;

public sealed record QuotationPdfDto(byte[] Content, string FileName);

public sealed record GetQuotationPdfQuery(Guid Id) : IRequest<ApiResult<QuotationPdfDto>>;

public sealed class GetQuotationPdfQueryHandler(
    IApplicationDbContext context,
    IQuotationPdfGenerator generator)
    : IRequestHandler<GetQuotationPdfQuery, ApiResult<QuotationPdfDto>>
{
    public async Task<ApiResult<QuotationPdfDto>> Handle(
        GetQuotationPdfQuery request,
        CancellationToken cancellationToken)
    {
        var quotation = await context.Quotations
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (quotation is null)
            return ApiResult<QuotationPdfDto>.Failure("Quotation not found.");

        var lines = quotation.Lines
            .OrderBy(line => line.SortOrder)
            .Select((line, index) => new QuotationPdfLine(
                index + 1,
                line.ItemName,
                line.ModelNo,
                line.Brand,
                line.Description,
                line.Quantity,
                line.UnitRate,
                line.DiscountPercent,
                line.TaxPercent,
                line.LineTotal))
            .ToList();

        var model = new QuotationPdfModel(
            quotation.QuoteNumber,
            quotation.CustomerName,
            quotation.CustomerCompany,
            quotation.CustomerEmail,
            quotation.CustomerPhone,
            quotation.CustomerAddress,
            quotation.IssueDate,
            quotation.ValidUntil,
            quotation.Status.ToString(),
            quotation.Notes,
            quotation.Terms,
            quotation.Subtotal,
            quotation.DiscountTotal,
            quotation.TaxTotal,
            quotation.GrandTotal,
            lines);

        return ApiResult<QuotationPdfDto>.Success(
            new QuotationPdfDto(generator.Generate(model), $"{quotation.QuoteNumber}.pdf"));
    }
}
