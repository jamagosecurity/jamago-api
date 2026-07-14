using Jama.Application.Interfaces;
using Jama.Domain.Entities;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Infrastructure.Repositories;

public class ContactRepository(AppDbContext db) : IContactRepository
{
    public async Task<IReadOnlyList<ContactSubmission>> GetAllAsync(CancellationToken ct = default) =>
        await db.ContactSubmissions
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<ContactSubmission> AddAsync(ContactSubmission entity, CancellationToken ct = default)
    {
        db.ContactSubmissions.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }
}
