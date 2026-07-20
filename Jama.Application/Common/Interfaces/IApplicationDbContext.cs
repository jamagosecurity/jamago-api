using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<AdminUser> AdminUsers { get; }
    DbSet<ContactSubmission> ContactSubmissions { get; }
    DbSet<Staff> Staff { get; }
    DbSet<DiaInspection> DiaInspections { get; }
    DbSet<DiaInspectionHistory> DiaInspectionHistory { get; }
    DbSet<TechnicianInspection> TechnicianInspections { get; }
    DbSet<TechnicianInspectionHistory> TechnicianInspectionHistory { get; }
    DbSet<InspectionInvoice> InspectionInvoices { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
