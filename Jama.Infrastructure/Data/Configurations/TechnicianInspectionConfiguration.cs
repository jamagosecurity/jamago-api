using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public sealed class TechnicianInspectionConfiguration : IEntityTypeConfiguration<TechnicianInspection>
{
    public void Configure(EntityTypeBuilder<TechnicianInspection> builder)
    {
        builder.ToTable("TechnicianInspections");
        builder.HasIndex(x => new { x.DiaInspectionId, x.Quarter, x.IsDeleted });
        builder.HasIndex(x => new { x.TechnicianId, x.Status });
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.DiaInspection).WithMany()
            .HasForeignKey(x => x.DiaInspectionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Network).WithOne(x => x.TechnicianInspection)
            .HasForeignKey<NetworkDetail>(x => x.TechnicianInspectionId);
        builder.HasOne(x => x.Vms).WithOne(x => x.TechnicianInspection)
            .HasForeignKey<VmsDetail>(x => x.TechnicianInspectionId);
        builder.HasOne(x => x.UpsGeneral).WithOne(x => x.TechnicianInspection)
            .HasForeignKey<UpsGeneralDetail>(x => x.TechnicianInspectionId);
        builder.HasOne(x => x.Anpr).WithOne(x => x.TechnicianInspection)
            .HasForeignKey<AnprConfiguration>(x => x.TechnicianInspectionId);
        builder.HasOne(x => x.Kpoi).WithOne(x => x.TechnicianInspection)
            .HasForeignKey<KpoiDetail>(x => x.TechnicianInspectionId);
    }
}

public sealed class CameraDetailConfiguration : IEntityTypeConfiguration<CameraDetail>
{
    public void Configure(EntityTypeBuilder<CameraDetail> builder)
    {
        builder.ToTable("CameraDetails");
        builder.Property(x => x.Brand).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Location).HasMaxLength(300);
        builder.Property(x => x.Remarks).HasMaxLength(500);
        builder.HasIndex(x => x.TechnicianInspectionId);
    }
}

public sealed class NetworkDetailConfiguration : IEntityTypeConfiguration<NetworkDetail>
{
    public void Configure(EntityTypeBuilder<NetworkDetail> builder)
    {
        builder.ToTable("NetworkDetails");
        builder.Property(x => x.SwitchBrand).HasMaxLength(120);
        builder.Property(x => x.SwitchModel).HasMaxLength(120);
        builder.Property(x => x.RouterBrand).HasMaxLength(120);
        builder.Property(x => x.RouterModel).HasMaxLength(120);
        builder.Property(x => x.Firewall).HasMaxLength(200);
        builder.Property(x => x.RackDetails).HasMaxLength(500);
        builder.Property(x => x.NetworkRemarks).HasMaxLength(500);
    }
}

public sealed class VmsDetailConfiguration : IEntityTypeConfiguration<VmsDetail>
{
    public void Configure(EntityTypeBuilder<VmsDetail> builder)
    {
        builder.ToTable("VmsDetails");
        builder.Property(x => x.VmsName).HasMaxLength(200);
        builder.Property(x => x.Version).HasMaxLength(100);
        builder.Property(x => x.LicenseDetails).HasMaxLength(500);
        builder.Property(x => x.ServerDetails).HasMaxLength(500);
        builder.Property(x => x.HealthStatus).HasMaxLength(100);
        builder.Property(x => x.Remarks).HasMaxLength(500);
    }
}

public sealed class UpsGeneralDetailConfiguration : IEntityTypeConfiguration<UpsGeneralDetail>
{
    public void Configure(EntityTypeBuilder<UpsGeneralDetail> builder)
    {
        builder.ToTable("UpsGeneralDetails");
        builder.Property(x => x.UpsBrand).HasMaxLength(120);
        builder.Property(x => x.UpsCapacity).HasMaxLength(120);
        builder.Property(x => x.BatteryStatus).HasMaxLength(120);
        builder.Property(x => x.GeneratorDetails).HasMaxLength(500);
        builder.Property(x => x.GeneralRemarks).HasMaxLength(500);
    }
}

public sealed class AnprConfigurationConfiguration : IEntityTypeConfiguration<AnprConfiguration>
{
    public void Configure(EntityTypeBuilder<AnprConfiguration> builder)
    {
        builder.ToTable("AnprConfigurations");
        builder.Property(x => x.CameraDetails).HasMaxLength(500);
        builder.Property(x => x.Configuration).HasMaxLength(500);
        builder.Property(x => x.SoftwareVersion).HasMaxLength(100);
        builder.Property(x => x.Remarks).HasMaxLength(500);
    }
}

public sealed class KpoiDetailConfiguration : IEntityTypeConfiguration<KpoiDetail>
{
    public void Configure(EntityTypeBuilder<KpoiDetail> builder)
    {
        builder.ToTable("KpoiDetails");
        builder.Property(x => x.IvdIvss).HasMaxLength(500);
        builder.Property(x => x.KpoiCamera).HasMaxLength(500);
        builder.Property(x => x.Lens).HasMaxLength(500);
        builder.Property(x => x.HardDisc).HasMaxLength(500);
    }
}

public sealed class InspectionInvoiceConfiguration : IEntityTypeConfiguration<InspectionInvoice>
{
    public void Configure(EntityTypeBuilder<InspectionInvoice> builder)
    {
        builder.ToTable("InspectionInvoices");
        builder.Property(x => x.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.InvoiceNumber).IsUnique();
        builder.HasIndex(x => new { x.DiaInspectionId, x.Quarter });
    }
}

public sealed class TechnicianInspectionHistoryConfiguration : IEntityTypeConfiguration<TechnicianInspectionHistory>
{
    public void Configure(EntityTypeBuilder<TechnicianInspectionHistory> builder)
    {
        builder.ToTable("TechnicianInspectionHistory");
        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ActorName).HasMaxLength(300);
        builder.Property(x => x.BeforeJson).HasColumnType("jsonb");
        builder.Property(x => x.AfterJson).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.TechnicianInspectionId, x.CreatedAt });
        builder.HasIndex(x => new { x.DiaInspectionId, x.CreatedAt });
    }
}
