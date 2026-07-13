using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jama.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<ContactSubmission> ContactSubmissions => Set<ContactSubmission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.FullName).HasMaxLength(150);
            entity.Property(x => x.Role).HasMaxLength(50);
        });

        modelBuilder.Entity<ContactSubmission>(entity =>
        {
            entity.Property(x => x.FullName).HasMaxLength(150);
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.Phone).HasMaxLength(30);
            entity.Property(x => x.Service).HasMaxLength(120);
            entity.Property(x => x.Status).HasMaxLength(30);
        });
    }
}
