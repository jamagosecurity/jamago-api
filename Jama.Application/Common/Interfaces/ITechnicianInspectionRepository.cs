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
    /// Atomically replaces every child-detail row for the inspection (cameras, network, VMS, UPS,
    /// ANPR, K'Poi) with the freshly built graph carried on <paramref name="inspection"/>, and bumps
    /// the parent's timestamp. All operations on pre-existing rows are set-based, so this is safe
    /// against duplicate/orphaned child rows and cannot raise a tracked-update concurrency error.
    /// The audit-history row added separately on the context is persisted in the same transaction.
    /// </summary>
    Task ReplaceInspectionDetailsAsync(TechnicianInspection inspection, CancellationToken cancellationToken);
}
