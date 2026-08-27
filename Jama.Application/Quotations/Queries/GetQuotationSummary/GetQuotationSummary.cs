using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Quotations.Queries.GetQuotationSummary;

public sealed record GetQuotationSummaryQuery : IRequest<ApiResult<QuotationSummaryDto>>;

public sealed class GetQuotationSummaryQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetQuotationSummaryQuery, ApiResult<QuotationSummaryDto>>
{
    public async Task<ApiResult<QuotationSummaryDto>> Handle(
        GetQuotationSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var quotations = context.Quotations.AsNoTracking();

        // Counted in one grouped round trip rather than five separate counts.
        var byStatus = await quotations
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Value = g.Sum(x => x.GrandTotal) })
            .ToListAsync(cancellationToken);

        int CountOf(QuotationStatus status) =>
            byStatus.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        decimal ValueOf(QuotationStatus status) =>
            byStatus.FirstOrDefault(x => x.Status == status)?.Value ?? 0m;

        return ApiResult<QuotationSummaryDto>.Success(new QuotationSummaryDto(
            byStatus.Sum(x => x.Count),
            CountOf(QuotationStatus.Draft),
            CountOf(QuotationStatus.Sent),
            CountOf(QuotationStatus.Accepted),
            ValueOf(QuotationStatus.Accepted),
            ValueOf(QuotationStatus.Draft) + ValueOf(QuotationStatus.Sent)));
    }
}
