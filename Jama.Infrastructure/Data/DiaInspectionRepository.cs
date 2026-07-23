using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jama.Infrastructure.Data;

public sealed class DiaInspectionRepository(ApplicationDbContext context) : IDiaInspectionRepository
{
    public IQueryable<DiaInspection> Inspections => context.DiaInspections;
    public IQueryable<DiaInspectionHistory> History => context.DiaInspectionHistory;

    public Task<DiaInspection?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        context.DiaInspections.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> DiaNumberExistsAsync(
        string normalizedDiaNumber,
        Guid? excludingId,
        CancellationToken cancellationToken) =>
        context.DiaInspections.AnyAsync(
            x => x.NormalizedDiaNumber == normalizedDiaNumber
                && (!excludingId.HasValue || x.Id != excludingId.Value),
            cancellationToken);

    public void Add(DiaInspection inspection) => context.DiaInspections.Add(inspection);
    public void AddHistory(DiaInspectionHistory history) => context.DiaInspectionHistory.Add(history);

    public async Task<IReadOnlyDictionary<Guid, int>> GetSubmittedQuarterCountsAsync(
        IReadOnlyCollection<Guid> diaInspectionIds, CancellationToken cancellationToken)
    {
        if (diaInspectionIds.Count == 0)
            return new Dictionary<Guid, int>();

        var counts = await context.TechnicianInspections.AsNoTracking()
            .Where(x => diaInspectionIds.Contains(x.DiaInspectionId)
                && x.Status == TechnicianInspectionStatus.Submitted
                && !x.IsDeleted)
            .GroupBy(x => x.DiaInspectionId)
            .Select(g => new { DiaInspectionId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.DiaInspectionId, x => x.Count);
    }
}

public sealed class UnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
