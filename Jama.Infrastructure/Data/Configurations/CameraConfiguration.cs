using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public sealed class CameraConfiguration : IEntityTypeConfiguration<Camera>
{
    public void Configure(EntityTypeBuilder<Camera> builder)
    {
        builder.ToTable("Cameras");

        builder.Property(x => x.ItemName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Brand).HasMaxLength(120).IsRequired();

        // Enums stored as their name rather than an ordinal, so a row still reads
        // "Dome" in psql and reordering an enum cannot silently remap old rows.
        // Free text now, not an enum name. 60 rather than 20: the old ceiling
        // fitted "Fisheye" and nothing a supplier actually writes.
        builder.Property(x => x.Type).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Uom).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Resolution).HasConversion<string>().HasMaxLength(20).IsRequired();
        // 3 places: sub-Mbps profiles are real, and the calculator multiplies by
        // 86 400 seconds, so a rounded input becomes a visibly wrong terabyte.
        builder.Property(x => x.BitrateMbps).HasPrecision(8, 3);
        builder.Property(x => x.ItemType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.WarrantyUnit).HasConversion<string>().HasMaxLength(10);

        // Required at the column level though optional to the user: blank comes in
        // as an empty string, never null, so the unique index below can rely on it.
        // See Camera.ModelNo.
        builder.Property(x => x.ModelNo).HasMaxLength(120).IsRequired();

        builder.Property(x => x.SearchKey).HasMaxLength(300);
        builder.Property(x => x.DescriptionEn).HasMaxLength(500);
        builder.Property(x => x.DescriptionAr).HasMaxLength(500);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.HsnCode).HasMaxLength(20);

        // Money and percentages get explicit precision. Left to convention they
        // would land on numeric with no scale, and a rate of 12.005 would come
        // back as 12.005 from one provider and 12.01 from another.
        builder.Property(x => x.SupplierCost).HasPrecision(18, 2);
        builder.Property(x => x.Rate).HasPrecision(18, 2);
        builder.Property(x => x.Margin).HasPrecision(9, 2);
        builder.Property(x => x.Discount).HasPrecision(5, 2);
        builder.Property(x => x.TaxRate).HasPrecision(5, 2);

        // One line per brand, type and model. This is the backstop for two writers
        // racing; CameraRules.ExistsAsync does the case-insensitive check that
        // produces a readable message. The index itself is case-sensitive, so it
        // catches exact collisions only — which is all the race can produce, since
        // both writers pass the same normalised values through the handler.
        builder.HasIndex(x => new { x.Brand, x.Type, x.ModelNo }).IsUnique();

        builder.HasMany(x => x.Images)
            .WithOne(x => x.Camera)
            .HasForeignKey(x => x.CameraId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CameraImageConfiguration : IEntityTypeConfiguration<CameraImage>
{
    public void Configure(EntityTypeBuilder<CameraImage> builder)
    {
        builder.ToTable("CameraImages");

        builder.Property(x => x.FileName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(400).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(150).IsRequired();

        // The gallery is always read by item, in display order.
        builder.HasIndex(x => new { x.CameraId, x.SortOrder });
    }
}
