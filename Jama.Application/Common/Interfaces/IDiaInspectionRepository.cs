using Jama.Domain.Entities;

namespace Jama.Application.Common.Interfaces;

public interface IDiaInspectionRepository
{
    IQueryable<DiaInspection> Inspections { get; }
    IQueryable<DiaInspectionHistory> History { get; }
    Task<DiaInspection?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> DiaNumberExistsAsync(string normalizedDiaNumber, Guid? excludingId, CancellationToken cancellationToken);
    void Add(DiaInspection inspection);
    void AddHistory(DiaInspectionHistory history);

    /// <summary>
    /// Returns, per DIA id, how many quarterly technician inspections have been submitted. Drives
    /// the admin-side quarter/status so it tracks the technicians' real progress (same rule the
    /// technician portal uses) rather than elapsed calendar time. DIA ids with no submissions are
    /// absent from the result (treat as 0).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetSubmittedQuarterCountsAsync(
        IReadOnlyCollection<Guid> diaInspectionIds, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
    Guid UserId { get; }
    string? DisplayName { get; }
}
