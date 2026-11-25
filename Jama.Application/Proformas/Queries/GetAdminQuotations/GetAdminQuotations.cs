using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Proformas.Queries.GetAdminQuotations;

/// <summary>
/// Every quotation, whoever made it and whatever its status — the administrator's list. Bound with [AsParameters],
/// so every optional parameter is nullable.
/// </summary>
public sealed record GetAdminQuotationsQuery : IRequest<ApiResult<PaginatedResult<AdminQuotationDto>>>
{
    public int? PageNumber { get; init; }
    public int? PageSize { get; init; }
    /// <summary>Matches the quotation number, PI number, project, site, client, phone or who made it.</summary>
    public string? Search { get; init; }
    public BoqStatus? Status { get; init; }
}

public sealed class GetAdminQuotationsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAdminQuotationsQuery, ApiResult<PaginatedResult<AdminQuotationDto>>>
{
    public async Task<ApiResult<PaginatedResult<AdminQuotationDto>>> Handle(GetAdminQuotationsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.PageNumber ?? 1);
        var size = Math.Clamp(request.PageSize ?? 25, 1, 100);

        var query = context.Boqs.AsNoTracking();

        if (request.Status is { } status)
            query = query.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim().ToLower();
            query = query.Where(x => x.BoqNumber.ToLower().Contains(s)
                || x.ProjectName.ToLower().Contains(s)
                || (x.SiteLocation != null && x.SiteLocation.ToLower().Contains(s))
                || (x.ClientName != null && x.ClientName.ToLower().Contains(s))
                || (x.ContactNumber != null && x.ContactNumber.ToLower().Contains(s))
                || (x.PreparedByName != null && x.PreparedByName.ToLower().Contains(s))
                || context.ProformaInvoices.Any(p => p.BoqId == x.Id
                    && (p.Number.ToLower().Contains(s)
                        || (p.PoNumber != null && p.PoNumber.ToLower().Contains(s))
                        || (p.CustomerName != null && p.CustomerName.ToLower().Contains(s)))));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new AdminQuotationDto(
                x.Id,
                x.BoqNumber,
                x.RevisionNumber,
                x.ProjectName,
                x.SiteLocation,
                x.ClientName,
                x.ContactNumber,
                x.IssueDate,
                x.Status,
                x.GrandTotal,
                x.PreparedByName,
                x.CreatedAt,
                context.ProformaInvoices
                    .Where(p => p.BoqId == x.Id)
                    .Select(p => new ProformaDto(p.Number, p.IssuedOn, p.IssuedByName, p.PoNumber, p.PoDate,
                        p.CustomerName, p.ProjectName, p.ContactNumber, p.Location))
                    .FirstOrDefault()))
            .ToListAsync(ct);

        var totalPages = (int)Math.Ceiling(total / (double)size);
        return ApiResult<PaginatedResult<AdminQuotationDto>>.Success(
            new PaginatedResult<AdminQuotationDto>(items, total, page, size, totalPages));
    }
}
