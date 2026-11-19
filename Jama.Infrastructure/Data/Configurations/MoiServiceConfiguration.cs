using Jama.Application.Maintenance;
using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public sealed class MoiServiceItemConfiguration : IEntityTypeConfiguration<MoiServiceItem>
{
    public void Configure(EntityTypeBuilder<MoiServiceItem> builder)
    {
        builder.ToTable("MoiServiceItems");

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

public sealed class MoiServiceImageConfiguration : IEntityTypeConfiguration<MoiServiceImage>
{
    public void Configure(EntityTypeBuilder<MoiServiceImage> builder)
    {
        builder.ToTable("MoiServiceImages");

        builder.Property(x => x.FileName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(300).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => new { x.CatalogueItemId, x.SortOrder });
    }
}
