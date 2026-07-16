using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<AdminUser> AdminUsers { get; }
    DbSet<ContactSubmission> ContactSubmissions { get; }
    DbSet<Staff> Staff { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
