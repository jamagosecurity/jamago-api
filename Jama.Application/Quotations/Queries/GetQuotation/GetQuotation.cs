using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Quotations.Queries.GetQuotation;

public sealed record GetQuotationQuery(Guid Id) : IRequest<ApiResult<QuotationDto>>;

public sealed class GetQuotationQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetQuotationQuery, ApiResult<QuotationDto>>
{
    public async Task<ApiResult<QuotationDto>> Handle(
        GetQuotationQuery request,
        CancellationToken cancellationToken)
    {
        var quotation = await context.Quotations
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        return quotation is null
            ? ApiResult<QuotationDto>.Failure("Quotation not found.")
            : ApiResult<QuotationDto>.Success(QuotationMappings.ToDto(quotation));
    }
}
