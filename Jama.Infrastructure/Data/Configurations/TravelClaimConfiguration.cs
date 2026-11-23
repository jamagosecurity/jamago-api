using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public sealed class TravelClaimConfiguration : IEntityTypeConfiguration<TravelClaim>
{
    public void Configure(EntityTypeBuilder<TravelClaim> builder)
    {
        builder.ToTable("TravelClaims");
        builder.Property(x => x.ClaimNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.ClaimNumber).IsUnique();
        builder.Property(x => x.ClaimantName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.TripType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.SiteName).HasMaxLength(200);
        builder.Property(x => x.Pickup).HasMaxLength(300);
        builder.Property(x => x.Dropoff).HasMaxLength(300);
        builder.Property(x => x.Total).HasPrecision(12, 2);
        builder.Property(x => x.ApprovedAmount).HasPrecision(12, 2);
        builder.Property(x => x.AdjustmentReason).HasMaxLength(300);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.ApprovedByName).HasMaxLength(300);
        builder.Property(x => x.RejectedByName).HasMaxLength(300);
        builder.Property(x => x.RejectReason).HasMaxLength(500);
        // PostgreSQL's xmin: a claim changed by someone else since it was read is not saved over.
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.ClaimantId, x.Status });
        builder.HasIndex(x => new { x.Status, x.SubmittedAt });
        builder.HasIndex(x => x.TripOn);
        builder.HasMany(x => x.Lines).WithOne(x => x.TravelClaim).HasForeignKey(x => x.TravelClaimId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.People).WithOne(x => x.TravelClaim).HasForeignKey(x => x.TravelClaimId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Events).WithOne(x => x.TravelClaim).HasForeignKey(x => x.TravelClaimId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Reimbursement).WithMany(x => x.Claims).HasForeignKey(x => x.ReimbursementId).OnDelete(DeleteBehavior.Restrict);
    }
}
