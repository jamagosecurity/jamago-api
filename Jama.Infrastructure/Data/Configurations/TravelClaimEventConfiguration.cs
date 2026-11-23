using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public sealed class TravelClaimEventConfiguration : IEntityTypeConfiguration<TravelClaimEvent>
{
    public void Configure(EntityTypeBuilder<TravelClaimEvent> builder)
    {
        builder.ToTable("TravelClaimEvents");
        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.ActorName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.Amount).HasPrecision(12, 2);
        builder.HasIndex(x => new { x.TravelClaimId, x.CreatedAt });
    }
}
