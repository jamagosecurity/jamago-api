using Jama.Domain.Entities;

namespace Jama.Application.Common.Interfaces;

public interface ITechnicianInspectionRepository
{
    IQueryable<DiaInspection> ActiveDiaInspections { get; }
    IQueryable<TechnicianInspection> Inspections { get; }
    IQueryable<TechnicianInspectionHistory> History { get; }
    IQueryable<InspectionInvoice> Invoices { get; }
    Task<DiaInspection?> FindActiveDiaAsync(Guid id, CancellationToken cancellationToken);
    Task<TechnicianInspection?> FindInspectionAsync(Guid id, CancellationToken cancellationToken);
    Task<TechnicianInspection?> FindInspectionForDraftAsync(Guid id, CancellationToken cancellationToken);
    Task<TechnicianInspection?> FindQuarterInspectionAsync(
        Guid diaInspectionId, int quarter, CancellationToken cancellationToken);
    Task<InspectionInvoice?> FindInvoiceForUpdateAsync(Guid id, CancellationToken cancellationToken);
    void Add(TechnicianInspection inspection);
    void AddHistory(TechnicianInspectionHistory history);
    void AddInvoice(InspectionInvoice invoice);

    /// <summary>
    /// Atomically removes every existing child-detail row for the inspection (cameras, network,
    /// VMS, UPS, ANPR, K'Poi) and persists the freshly built graph currently tracked on the
    /// context. Set-based deletes make this safe against duplicate or orphaned child rows.
    /// </summary>
    Task ReplaceInspectionDetailsAsync(Guid inspectionId, CancellationToken cancellationToken);
}
