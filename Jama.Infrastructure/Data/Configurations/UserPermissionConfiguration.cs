using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.Property(x => x.Permission)
            .HasMaxLength(100)
            .IsRequired();

        // One row per permission per account — makes granting idempotent.
        builder.HasIndex(x => new { x.AdminUserId, x.Permission }).IsUnique();

        builder.HasOne(x => x.User)
            .WithMany(x => x.Permissions)
            .HasForeignKey(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
