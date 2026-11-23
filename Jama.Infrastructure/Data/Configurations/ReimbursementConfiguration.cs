using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public sealed class ReimbursementConfiguration : IEntityTypeConfiguration<Reimbursement>
{
    public void Configure(EntityTypeBuilder<Reimbursement> builder)
    {
        builder.ToTable("Reimbursements");
        builder.Property(x => x.Number).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Number).IsUnique();
        builder.Property(x => x.PayeeName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(12, 2);
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Reference).HasMaxLength(300);
        builder.Property(x => x.PaidByName).HasMaxLength(300).IsRequired();
        builder.HasIndex(x => new { x.PayeeId, x.PaidAt });
    }
}
