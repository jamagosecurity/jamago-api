using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public sealed class NonMoiJobConfiguration : IEntityTypeConfiguration<NonMoiJob>
{
    public void Configure(EntityTypeBuilder<NonMoiJob> builder)
    {
        // The same pin guard a DIA site carries: half a pin, or one out of range,
        // sends a technician to the wrong place.
        builder.ToTable("NonMoiJobs", t => t.HasCheckConstraint(
            "CK_NonMoiJobs_SitePin",
            """("Latitude" IS NULL) = ("Longitude" IS NULL) AND ("Latitude" IS NULL OR ("Latitude" BETWEEN -90 AND 90 AND "Longitude" BETWEEN -180 AND 180))"""));

        builder.Property(x => x.JobNumber).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => x.JobNumber).IsUnique();
        builder.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CustomerNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Location).HasMaxLength(300).IsRequired();
        builder.Property(x => x.DescriptionEn).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.DescriptionAr).HasMaxLength(2000);
        builder.Property(x => x.AssignedToName).HasMaxLength(300).IsRequired();

        builder.HasIndex(x => x.AssignedToId);

        builder.Property(x => x.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Assigned");
        builder.Property(x => x.WorkTotal).HasPrecision(12, 2);
        builder.Property(x => x.CompletionNotes).HasMaxLength(1000);
        builder.HasMany(x => x.Lines).WithOne(x => x.NonMoiJob).HasForeignKey(x => x.NonMoiJobId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class NonMoiJobLineConfiguration : IEntityTypeConfiguration<NonMoiJobLine>
{
    public void Configure(EntityTypeBuilder<NonMoiJobLine> builder)
    {
        builder.ToTable("NonMoiJobLines");
        builder.Property(x => x.Category).HasMaxLength(100).IsRequired();
        builder.Property(x => x.IssueType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.UnitFee).HasPrecision(12, 2);
        builder.Property(x => x.LineTotal).HasPrecision(12, 2);
        builder.HasIndex(x => x.NonMoiJobId);
    }
}
