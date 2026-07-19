using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jama.Infrastructure.Data.Configurations;

public class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> builder)
    {
        builder.Property(x => x.FullName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Role)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.Responsibility)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.Department)
            .HasMaxLength(120);

        builder.HasIndex(x => x.DisplayOrder);

        builder.HasIndex(x => x.AdminUserId)
            .IsUnique();

        builder.HasOne(x => x.Account)
            .WithOne(x => x.StaffProfile)
            .HasForeignKey<Staff>(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
