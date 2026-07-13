using Jama.Application.Common;
using Jama.Application.Interfaces;
using Jama.Domain.Entities;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Infrastructure.Repositories;

public class ContactRepository(AppDbContext db) : IContactRepository
{
    public async Task<(IReadOnlyList<ContactSubmission> Items, int TotalCount)> GetPagedAsync(
        PaginationQuery query,
        CancellationToken ct = default)
    {
        var source = db.ContactSubmissions.AsNoTracking().OrderByDescending(x => x.CreatedAt);
        var total = await source.CountAsync(ct);
        var items = await source
            .Skip(query.Skip)
            .Take(query.NormalizedPageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<ContactSubmission> AddAsync(ContactSubmission entity, CancellationToken ct = default)
    {
        db.ContactSubmissions.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }
}
