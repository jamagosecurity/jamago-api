using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Quotations.Queries.GetQuotations;

/// <summary>
/// Bound with [AsParameters]. Every optional parameter must be nullable: a
/// non-nullable value type is treated as a REQUIRED query parameter and the
/// property initialiser is never consulted.
/// </summary>
public sealed record GetQuotationsQuery : IRequest<ApiResult<PaginatedResult<QuotationListItemDto>>>
{
    public int? PageNumber { get; init; }
    public int? PageSize { get; init; }
    /// <summary>Matches on quote number, customer name or company.</summary>
    public string? Search { get; init; }
    public QuotationStatus? Status { get; init; }
}

public sealed class GetQuotationsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetQuotationsQuery, ApiResult<PaginatedResult<QuotationListItemDto>>>
{
    public async Task<ApiResult<PaginatedResult<QuotationListItemDto>>> Handle(
        GetQuotationsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.PageNumber ?? 1);
        var size = Math.Clamp(request.PageSize ?? 20, 1, 100);

        var query = context.Quotations.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(x => x.QuoteNumber.ToLower().Contains(search)
                || x.CustomerName.ToLower().Contains(search)
                || (x.CustomerCompany != null && x.CustomerCompany.ToLower().Contains(search)));
        }

        if (request.Status is { } status)
            query = query.Where(x => x.Status == status);

        var total = await query.CountAsync(cancellationToken);

        // Newest first: a quotation list is a worklist, and the one just written
        // is the one most likely to be wanted again.
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(QuotationMappings.ListProjection)
            .ToListAsync(cancellationToken);

        var totalPages = size == 0 ? 0 : (int)Math.Ceiling(total / (double)size);

        return ApiResult<PaginatedResult<QuotationListItemDto>>.Success(
            new PaginatedResult<QuotationListItemDto>(items, total, page, size, totalPages));
    }
}
