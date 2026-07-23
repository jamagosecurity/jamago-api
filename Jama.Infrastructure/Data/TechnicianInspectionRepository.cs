using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jama.Infrastructure.Data;

public sealed class TechnicianInspectionRepository(ApplicationDbContext context) : ITechnicianInspectionRepository
{
    public IQueryable<DiaInspection> ActiveDiaInspections =>
        context.DiaInspections.AsNoTracking().Where(x => x.IsActive && !x.IsArchived);

    public IQueryable<TechnicianInspection> Inspections =>
        context.TechnicianInspections.Where(x => !x.IsDeleted);

    public IQueryable<TechnicianInspectionHistory> History =>
        context.TechnicianInspectionHistory.AsNoTracking();

    public IQueryable<InspectionInvoice> Invoices =>
        context.InspectionInvoices.AsNoTracking();

    public Task<DiaInspection?> FindActiveDiaAsync(Guid id, CancellationToken cancellationToken) =>
        context.DiaInspections.FirstOrDefaultAsync(
            x => x.Id == id && x.IsActive && !x.IsArchived, cancellationToken);

    public Task<TechnicianInspection?> FindInspectionAsync(Guid id, CancellationToken cancellationToken) =>
        context.TechnicianInspections
            .Include(x => x.Cameras)
            .Include(x => x.Network)
            .Include(x => x.Vms)
            .Include(x => x.UpsGeneral)
            .Include(x => x.Anpr)
            .Include(x => x.Kpoi)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

    public Task<TechnicianInspection?> FindQuarterInspectionAsync(
        Guid diaInspectionId, int quarter, CancellationToken cancellationToken) =>
        context.TechnicianInspections
            .Include(x => x.Cameras)
            .Include(x => x.Network)
            .Include(x => x.Vms)
            .Include(x => x.UpsGeneral)
            .Include(x => x.Anpr)
            .Include(x => x.Kpoi)
            .FirstOrDefaultAsync(
                x => x.DiaInspectionId == diaInspectionId && x.Quarter == quarter && !x.IsDeleted,
                cancellationToken);

    public Task<TechnicianInspection?> FindInspectionForDraftAsync(Guid id, CancellationToken cancellationToken) =>
        context.TechnicianInspections
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

    public Task<InspectionInvoice?> FindInvoiceForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.InspectionInvoices.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public void Add(TechnicianInspection inspection) => context.TechnicianInspections.Add(inspection);

    public void AddHistory(TechnicianInspectionHistory history) =>
        context.TechnicianInspectionHistory.Add(history);

    public void AddInvoice(InspectionInvoice invoice) => context.InspectionInvoices.Add(invoice);

    public async Task ReplaceInspectionDetailsAsync(Guid inspectionId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // Wipe existing child rows set-based (also clears any duplicates/orphans), then let
        // SaveChanges insert the freshly built rows the context is already tracking.
        await context.Set<CameraDetail>()
            .Where(x => x.TechnicianInspectionId == inspectionId).ExecuteDeleteAsync(cancellationToken);
        await context.Set<NetworkDetail>()
            .Where(x => x.TechnicianInspectionId == inspectionId).ExecuteDeleteAsync(cancellationToken);
        await context.Set<VmsDetail>()
            .Where(x => x.TechnicianInspectionId == inspectionId).ExecuteDeleteAsync(cancellationToken);
        await context.Set<UpsGeneralDetail>()
            .Where(x => x.TechnicianInspectionId == inspectionId).ExecuteDeleteAsync(cancellationToken);
        await context.Set<AnprConfiguration>()
            .Where(x => x.TechnicianInspectionId == inspectionId).ExecuteDeleteAsync(cancellationToken);
        await context.Set<KpoiDetail>()
            .Where(x => x.TechnicianInspectionId == inspectionId).ExecuteDeleteAsync(cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
