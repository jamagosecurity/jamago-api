using Jama.Application.Common;
using Jama.Application.Interfaces;
using Jama.Application.Options;
using Jama.Domain.Entities;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jama.Infrastructure.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        await db.Database.EnsureCreatedAsync(ct);
        await SeedAdminUserAsync(db, config, passwordHasher, logger, ct);
    }

    private static async Task SeedAdminUserAsync(
        AppDbContext db,
        IConfiguration config,
        IPasswordHasher passwordHasher,
        ILogger logger,
        CancellationToken ct)
    {
        if (await db.AdminUsers.AnyAsync(ct))
        {
            return;
        }

        var seed = config.GetSection(AdminSeedSettings.SectionName).Get<AdminSeedSettings>();
        if (seed is null ||
            string.IsNullOrWhiteSpace(seed.Email) ||
            string.IsNullOrWhiteSpace(seed.Password))
        {
            logger.LogWarning("No admin user exists and AdminSeed is not configured. Skipping admin seed.");
            return;
        }

        var user = new AdminUser
        {
            Email = seed.Email.Trim().ToLowerInvariant(),
            FullName = seed.FullName.Trim(),
            Role = Roles.Admin,
            IsActive = true,
            PasswordHash = passwordHasher.Hash(new AdminUser(), seed.Password),
        };

        db.AdminUsers.Add(user);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded default admin user {Email}", user.Email);
    }
}
