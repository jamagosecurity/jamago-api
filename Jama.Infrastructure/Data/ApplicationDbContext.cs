using System.Reflection;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jama.Infrastructure.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<ContactSubmission> ContactSubmissions => Set<ContactSubmission>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<DiaInspection> DiaInspections => Set<DiaInspection>();
    public DbSet<DiaInspectionHistory> DiaInspectionHistory => Set<DiaInspectionHistory>();
    public DbSet<TechnicianInspection> TechnicianInspections => Set<TechnicianInspection>();
    public DbSet<TechnicianInspectionHistory> TechnicianInspectionHistory => Set<TechnicianInspectionHistory>();
    public DbSet<InspectionInvoice> InspectionInvoices => Set<InspectionInvoice>();
    public DbSet<VipClient> VipClients => Set<VipClient>();
    public DbSet<VipClientFolder> VipClientFolders => Set<VipClientFolder>();
    public DbSet<VipClientDocument> VipClientDocuments => Set<VipClientDocument>();
    public DbSet<Camera> Cameras => Set<Camera>();
    public DbSet<CameraImage> CameraImages => Set<CameraImage>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationLine> QuotationLines => Set<QuotationLine>();
    public DbSet<Boq> Boqs => Set<Boq>();
    public DbSet<BoqSection> BoqSections => Set<BoqSection>();
    public DbSet<BoqLine> BoqLines => Set<BoqLine>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // The audit trail is append-only so nobody can rewrite the record of what
        // happened to a DIA. Deleting the DIA itself is the one exception: its
        // history describes a record that no longer exists, and the foreign key
        // is Restrict, so the rows have to go with it. The exception is scoped to
        // exactly that — history may only be removed alongside its own parent, so
        // entries belonging to a surviving record stay untouchable.
        var deletedInspectionIds = ChangeTracker.Entries<DiaInspection>()
            .Where(x => x.State is EntityState.Deleted)
            .Select(x => x.Entity.Id)
            .ToHashSet();

        if (ChangeTracker.Entries<DiaInspectionHistory>()
            .Any(x => x.State is EntityState.Modified
                || (x.State is EntityState.Deleted
                    && !deletedInspectionIds.Contains(x.Entity.DiaInspectionId))))
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
