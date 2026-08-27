using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public sealed class BoqConfiguration : IEntityTypeConfiguration<Boq>
{
    public void Configure(EntityTypeBuilder<Boq> builder)
    {
        builder.ToTable("Boqs");

        builder.Property(x => x.BoqNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ProjectName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SiteLocation).HasMaxLength(200);
        builder.Property(x => x.ClientName).HasMaxLength(200);
        builder.Property(x => x.ContactNumber).HasMaxLength(40);
        builder.Property(x => x.PreparedByName).HasMaxLength(200);
        builder.Property(x => x.Notes).HasMaxLength(2000);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Total).HasPrecision(18, 2);

        // The reference may already be circulating, so no two may share one. Also
        // the backstop for two writers allocating a number at the same moment.
        builder.HasIndex(x => x.BoqNumber).IsUnique();
        builder.HasIndex(x => x.PreparedById);

        builder.HasMany(x => x.Sections)
            .WithOne(x => x.Boq)
            .HasForeignKey(x => x.BoqId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BoqSectionConfiguration : IEntityTypeConfiguration<BoqSection>
{
    public void Configure(EntityTypeBuilder<BoqSection> builder)
    {
        builder.ToTable("BoqSections");

        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.BoqId, x.SortOrder });

        builder.HasMany(x => x.Lines)
            .WithOne(x => x.Section)
            .HasForeignKey(x => x.BoqSectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BoqLineConfiguration : IEntityTypeConfiguration<BoqLine>
{
    public void Configure(EntityTypeBuilder<BoqLine> builder)
    {
        builder.ToTable("BoqLines");

        builder.Property(x => x.ItemName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ModelNo).HasMaxLength(120);
        builder.Property(x => x.Brand).HasMaxLength(120);
        builder.Property(x => x.Uom).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(x => x.Quantity).HasPrecision(14, 2);
        builder.Property(x => x.UnitRate).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);

        builder.HasIndex(x => new { x.BoqSectionId, x.SortOrder });

        // SetNull, not Cascade: retiring a stock item must never delete lines off
        // a bill someone has approved. The line keeps its copied detail and rate.
        builder.HasOne(x => x.Camera)
            .WithMany()
            .HasForeignKey(x => x.CameraId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
