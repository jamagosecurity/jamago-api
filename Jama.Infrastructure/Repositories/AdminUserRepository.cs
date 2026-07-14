using Jama.Application.Interfaces;
using Jama.Domain.Entities;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Infrastructure.Repositories;

public class AdminUserRepository(AppDbContext db) : IAdminUserRepository
{
    public async Task<AdminUser?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.AdminUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<AdminUser?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        await db.AdminUsers.FirstOrDefaultAsync(x => x.Email == email, ct);

    public async Task<bool> AnyAsync(CancellationToken ct = default) =>
        await db.AdminUsers.AnyAsync(ct);

    public async Task AddAsync(AdminUser user, CancellationToken ct = default)
    {
        db.AdminUsers.Add(user);
        await db.SaveChangesAsync(ct);
    }
}
