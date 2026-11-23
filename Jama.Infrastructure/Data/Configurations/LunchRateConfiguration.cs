using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public sealed class LunchRateConfiguration : IEntityTypeConfiguration<LunchRate>
{
    public void Configure(EntityTypeBuilder<LunchRate> builder)
    {
        builder.ToTable("LunchRates");
        builder.Property(x => x.AmountPerPerson).HasPrecision(12, 2);
        builder.Property(x => x.SetByName).HasMaxLength(300).IsRequired();
        builder.HasIndex(x => x.EffectiveFrom);
    }
}
