using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public sealed class DiaInspectionConfiguration : IEntityTypeConfiguration<DiaInspection>
{
    public void Configure(EntityTypeBuilder<DiaInspection> builder)
    {
        builder.ToTable("DiaInspections");
        builder.Property(x => x.DiaNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NormalizedDiaNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ClientNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ClientName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ClientLocation).HasMaxLength(300).IsRequired();
        builder.HasIndex(x => x.NormalizedDiaNumber).IsUnique();
        builder.HasIndex(x => new { x.IsArchived, x.IsActive });
        builder.HasIndex(x => x.ActivatedDate);
    }
}

public sealed class DiaInspectionHistoryConfiguration : IEntityTypeConfiguration<DiaInspectionHistory>
{
    public void Configure(EntityTypeBuilder<DiaInspectionHistory> builder)
    {
        builder.ToTable("DiaInspectionHistory");
        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.ActorName).HasMaxLength(300);
        builder.Property(x => x.BeforeJson).HasColumnType("jsonb");
        builder.Property(x => x.AfterJson).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.DiaInspectionId, x.CreatedAt });
        builder.HasIndex(x => new { x.Action, x.CreatedAt });
        builder.HasOne(x => x.DiaInspection).WithMany(x => x.History)
            .HasForeignKey(x => x.DiaInspectionId).OnDelete(DeleteBehavior.Restrict);
    }
}
