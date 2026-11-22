using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public sealed class CashHandoverConfiguration : IEntityTypeConfiguration<CashHandover>
{
    public void Configure(EntityTypeBuilder<CashHandover> builder)
    {
        builder.ToTable("CashHandovers", t => t.HasCheckConstraint("CK_CashHandovers_Kind", "\"Kind\" IN ('MOI', 'NonMOI')"));
        builder.Property(x => x.Kind).HasMaxLength(10).IsRequired();
        builder.Property(x => x.CollectorName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.ExpectedAmount).HasPrecision(12, 2);
        builder.Property(x => x.ReceivedAmount).HasPrecision(12, 2);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.ReceivedByName).HasMaxLength(300).IsRequired();
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.Kind, x.CollectorId });
    }
}
