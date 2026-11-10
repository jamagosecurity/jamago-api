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
    DbSet<Boq> Boqs { get; }
    DbSet<BoqSection> BoqSections { get; }
    DbSet<BoqLine> BoqLines { get; }
    DbSet<BoqApprovalEvent> BoqApprovalEvents { get; }
    DbSet<Drawing> Drawings { get; }
    DbSet<DrawingFile> DrawingFiles { get; }
    DbSet<DrawingApprovalEvent> DrawingApprovalEvents { get; }
    DbSet<ClientRegistration> ClientRegistrations { get; }
    DbSet<MaintenanceRequest> MaintenanceRequests { get; }
    DbSet<MaintenanceRequestLine> MaintenanceRequestLines { get; }
    DbSet<MaintenanceCatalogueItem> MaintenanceCatalogueItems { get; }
    DbSet<MaintenanceCatalogueImage> MaintenanceCatalogueImages { get; }
    DbSet<NonMoiJob> NonMoiJobs { get; }
    DbSet<NonMoiJobLine> NonMoiJobLines { get; }
    DbSet<NonMoiJobEvent> NonMoiJobEvents { get; }
    DbSet<MailMessage> MailMessages { get; }
    DbSet<MailAccountState> MailAccountStates { get; }
    DbSet<MailSendLog> MailSendLogs { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
