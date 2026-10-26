using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public sealed class ClientRegistrationConfiguration : IEntityTypeConfiguration<ClientRegistration>
{
    public void Configure(EntityTypeBuilder<ClientRegistration> builder)
    {
        builder.ToTable("ClientRegistrations");

        // Sized to fit the DIA inspection columns they are copied into when a
        // client is accepted as MOI (name 200, number 100, location 300), and
        // the account's own FullName (150) — the tighter of the two wins.
        builder.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Mobile).HasMaxLength(40).IsRequired();
        builder.Property(x => x.SiteLocation).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ReviewedByName).HasMaxLength(300);
        builder.Property(x => x.RejectionReason).HasMaxLength(1000);

        // One registration per account: login resolves "why is this account
        // off" through it, and a second row would make that ambiguous.
        builder.HasIndex(x => x.AdminUserId).IsUnique();
        builder.HasIndex(x => new { x.Status, x.CreatedAt });

        builder.HasOne(x => x.Account)
            .WithOne()
            .HasForeignKey<ClientRegistration>(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.DiaInspection)
            .WithMany()
            .HasForeignKey(x => x.DiaInspectionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
