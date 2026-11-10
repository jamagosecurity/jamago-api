using Jama.Application.Maintenance;
using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public class MaintenanceRequestConfiguration : IEntityTypeConfiguration<MaintenanceRequest>
{
    public void Configure(EntityTypeBuilder<MaintenanceRequest> builder)
    {
        builder.Property(x => x.RequestNumber).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => x.RequestNumber).IsUnique();

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.SiteLocation).HasMaxLength(400);
        builder.Property(x => x.ClientName).HasMaxLength(200);
        builder.Property(x => x.ClientEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ContactNumber).HasMaxLength(50);
        builder.Property(x => x.ResolutionNotes).HasMaxLength(2000);
        builder.Property(x => x.CancellationReason).HasMaxLength(500);
        builder.Property(x => x.Subtotal).HasPrecision(12, 2);
        builder.Property(x => x.Discount).HasPrecision(12, 2);
        builder.Property(x => x.ServiceFee).HasPrecision(12, 2);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ReportedById);
        builder.HasIndex(x => x.AssignedToId);

        // Cascade: a line is part of the ticket, not a reference to anything
        // else — the same reasoning as a BOQ section's lines.
        builder.HasMany(x => x.Lines)
            .WithOne(x => x.MaintenanceRequest)
            .HasForeignKey(x => x.MaintenanceRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MaintenanceCatalogueItemConfiguration : IEntityTypeConfiguration<MaintenanceCatalogueItem>
{
    public void Configure(EntityTypeBuilder<MaintenanceCatalogueItem> builder)
    {
        builder.ToTable("MaintenanceCatalogueItems");

        builder.Property(x => x.Category).HasMaxLength(MaintenanceCategories.MaxLength).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        // 500 to match the stock inventory's descriptions.
        builder.Property(x => x.DescriptionEn).HasMaxLength(500);
        builder.Property(x => x.DescriptionAr).HasMaxLength(500);
        builder.Property(x => x.Price).HasPrecision(10, 2);

        // Backstop only — the commands refuse a duplicate case-insensitively
        // before it gets this far.
        builder.HasIndex(x => new { x.Category, x.Name }).IsUnique();

        builder.HasMany(x => x.Images)
            .WithOne(x => x.CatalogueItem)
            .HasForeignKey(x => x.CatalogueItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MaintenanceCatalogueImageConfiguration : IEntityTypeConfiguration<MaintenanceCatalogueImage>
{
    public void Configure(EntityTypeBuilder<MaintenanceCatalogueImage> builder)
    {
        builder.ToTable("MaintenanceCatalogueImages");

        builder.Property(x => x.FileName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(300).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => new { x.CatalogueItemId, x.SortOrder });
    }
}

public sealed class MaintenanceRequestLineConfiguration : IEntityTypeConfiguration<MaintenanceRequestLine>
{
    public void Configure(EntityTypeBuilder<MaintenanceRequestLine> builder)
    {
        builder.ToTable("MaintenanceRequestLines");

        builder.Property(x => x.Category).HasMaxLength(MaintenanceCategories.MaxLength).IsRequired();
        builder.Property(x => x.IssueType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DescriptionEn).HasMaxLength(500);
        builder.Property(x => x.DescriptionAr).HasMaxLength(500);
        builder.Property(x => x.UnitFee).HasPrecision(10, 2);
        builder.Property(x => x.CatalogueFee).HasPrecision(10, 2);
        builder.Property(x => x.LineTotal).HasPrecision(12, 2);

        builder.HasIndex(x => new { x.MaintenanceRequestId, x.SortOrder });
        builder.HasIndex(x => x.Category);
    }
}
