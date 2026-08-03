using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Technician.Queries.GetTechnicianInvoices;

public sealed record GetTechnicianInvoicesQuery : IRequest<ApiResult<IReadOnlyList<InspectionInvoiceDto>>>
{
    public Guid? DiaId { get; init; }
}

public sealed class GetTechnicianInvoicesHandler(ITechnicianInspectionRepository repository)
    : IRequestHandler<GetTechnicianInvoicesQuery, ApiResult<IReadOnlyList<InspectionInvoiceDto>>>
{
    public async Task<ApiResult<IReadOnlyList<InspectionInvoiceDto>>> Handle(
        GetTechnicianInvoicesQuery request,
        CancellationToken cancellationToken)
    {
        var query = repository.Invoices;
        if (request.DiaId is { } diaId)
            query = query.Where(x => x.DiaInspectionId == diaId);

        var items = await query
            .Join(repository.ActiveDiaInspections,
                invoice => invoice.DiaInspectionId,
                dia => dia.Id,
                (invoice, dia) => new { invoice, dia.DiaNumber })
            .OrderByDescending(x => x.invoice.GeneratedAt)
            .Select(x => new InspectionInvoiceDto(
                x.invoice.Id,
                x.invoice.TechnicianInspectionId,
                x.invoice.DiaInspectionId,
                x.DiaNumber,
                x.invoice.Quarter,
                x.invoice.InvoiceNumber,
                x.invoice.GeneratedAt))
            .ToListAsync(cancellationToken);

        return ApiResult<IReadOnlyList<InspectionInvoiceDto>>.Success(items);
    }
}
