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
