using System.Reflection;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jama.Infrastructure.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<ContactSubmission> ContactSubmissions => Set<ContactSubmission>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<DiaInspection> DiaInspections => Set<DiaInspection>();
    public DbSet<DiaInspectionHistory> DiaInspectionHistory => Set<DiaInspectionHistory>();
    public DbSet<TechnicianInspection> TechnicianInspections => Set<TechnicianInspection>();
    public DbSet<TechnicianInspectionHistory> TechnicianInspectionHistory => Set<TechnicianInspectionHistory>();
    public DbSet<InspectionInvoice> InspectionInvoices => Set<InspectionInvoice>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (ChangeTracker.Entries<DiaInspectionHistory>()
            .Any(x => x.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("DIA inspection audit records are immutable.");
        }

        if (ChangeTracker.Entries<TechnicianInspectionHistory>()
            .Any(x => x.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Technician inspection audit records are immutable.");
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
