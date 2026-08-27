using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<AdminUser> AdminUsers { get; }
    DbSet<UserPermission> UserPermissions { get; }
    DbSet<ContactSubmission> ContactSubmissions { get; }
    DbSet<Staff> Staff { get; }
    DbSet<DiaInspection> DiaInspections { get; }
    DbSet<DiaInspectionHistory> DiaInspectionHistory { get; }
    DbSet<TechnicianInspection> TechnicianInspections { get; }
    DbSet<TechnicianInspectionHistory> TechnicianInspectionHistory { get; }
    DbSet<InspectionInvoice> InspectionInvoices { get; }
    DbSet<VipClient> VipClients { get; }
    DbSet<VipClientFolder> VipClientFolders { get; }
    DbSet<VipClientDocument> VipClientDocuments { get; }
    DbSet<Camera> Cameras { get; }
    DbSet<CameraImage> CameraImages { get; }
    DbSet<Quotation> Quotations { get; }
    DbSet<QuotationLine> QuotationLines { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
