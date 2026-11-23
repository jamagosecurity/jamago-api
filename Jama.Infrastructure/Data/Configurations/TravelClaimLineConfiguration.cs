using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public sealed class TravelClaimLineConfiguration : IEntityTypeConfiguration<TravelClaimLine>
{
    public void Configure(EntityTypeBuilder<TravelClaimLine> builder)
    {
        builder.ToTable("TravelClaimLines");
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(x => x.BillAmount).HasPrecision(12, 2);
        builder.Property(x => x.Amount).HasPrecision(12, 2);
        builder.Property(x => x.RatePerPerson).HasPrecision(12, 2);
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(400).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.FileHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.FileHash);
    }
}
