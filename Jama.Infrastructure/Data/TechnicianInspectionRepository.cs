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
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

    public Task<InspectionInvoice?> FindInvoiceForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.InspectionInvoices.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public void Add(TechnicianInspection inspection) => context.TechnicianInspections.Add(inspection);

    public void AddHistory(TechnicianInspectionHistory history) =>
        context.TechnicianInspectionHistory.Add(history);

    public void AddInvoice(InspectionInvoice invoice) => context.InspectionInvoices.Add(invoice);

    public async Task ReplaceInspectionDetailsAsync(TechnicianInspection inspection, CancellationToken cancellationToken)
    {
        var id = inspection.Id;
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // Everything touching pre-existing rows is set-based so it cannot trip the change-tracker's
        // "expected 1 row, affected 0" optimistic-concurrency check (which mis-fires on the .NET 10
        // preview EF/Npgsql stack when a tracked UPDATE follows ExecuteDelete in one transaction).
        // Clear old child rows (also removes any duplicates/orphans)...
        await context.Set<CameraDetail>()
            .Where(x => x.TechnicianInspectionId == id).ExecuteDeleteAsync(cancellationToken);
        await context.Set<NetworkDetail>()
            .Where(x => x.TechnicianInspectionId == id).ExecuteDeleteAsync(cancellationToken);
        await context.Set<VmsDetail>()
            .Where(x => x.TechnicianInspectionId == id).ExecuteDeleteAsync(cancellationToken);
        await context.Set<UpsGeneralDetail>()
            .Where(x => x.TechnicianInspectionId == id).ExecuteDeleteAsync(cancellationToken);
        await context.Set<AnprConfiguration>()
            .Where(x => x.TechnicianInspectionId == id).ExecuteDeleteAsync(cancellationToken);
        await context.Set<KpoiDetail>()
            .Where(x => x.TechnicianInspectionId == id).ExecuteDeleteAsync(cancellationToken);

        // ...bump the parent timestamp set-based (the parent itself is loaded no-tracking, so it is
        // never part of SaveChanges)...
        await context.TechnicianInspections
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedAt, inspection.UpdatedAt), cancellationToken);

        // ...and insert the freshly built child rows explicitly. SaveChanges now issues INSERTs only
        // (these child rows plus the audit-history row added by the handler), which cannot raise the
        // affect-0 concurrency error.
        if (inspection.Cameras.Count > 0) context.Set<CameraDetail>().AddRange(inspection.Cameras);
        if (inspection.Network is not null) context.Set<NetworkDetail>().Add(inspection.Network);
        if (inspection.Vms is not null) context.Set<VmsDetail>().Add(inspection.Vms);
        if (inspection.UpsGeneral is not null) context.Set<UpsGeneralDetail>().Add(inspection.UpsGeneral);
        if (inspection.Anpr is not null) context.Set<AnprConfiguration>().Add(inspection.Anpr);
        if (inspection.Kpoi is not null) context.Set<KpoiDetail>().Add(inspection.Kpoi);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
