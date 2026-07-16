using Jama.Application.Auth;
using Jama.Application.Common;
using Jama.Application.Options;
using Jama.Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jama.Infrastructure.Data;

public static class InitialiserExtensions
{
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

        await initialiser.InitialiseAsync();
        await initialiser.SeedAsync();
    }
}

public class ApplicationDbContextInitialiser
{
    private readonly ILogger<ApplicationDbContextInitialiser> _logger;
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IPasswordHasher _passwordHasher;

    public ApplicationDbContextInitialiser(
        ILogger<ApplicationDbContextInitialiser> logger,
        ApplicationDbContext context,
        IConfiguration configuration,
        IPasswordHasher passwordHasher)
    {
        _logger = logger;
        _context = context;
        _configuration = configuration;
        _passwordHasher = passwordHasher;
    }

    public async Task InitialiseAsync()
    {
        try
        {
            await _context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            // Databases created with EnsureCreated (before Staff / migrations) already have
            // AdminUsers + ContactSubmissions, so InitialCreate fails and Staff never appears.
            _logger.LogWarning(
                ex,
                "MigrateAsync failed. Applying repair for legacy EnsureCreated schema.");

            await RepairLegacyEnsureCreatedSchemaAsync();
            await _context.Database.MigrateAsync();
        }
    }

    private async Task RepairLegacyEnsureCreatedSchemaAsync()
    {
        await _context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "Staff" (
                "Id" uuid NOT NULL,
                "FullName" character varying(150) NOT NULL,
                "Role" character varying(120) NOT NULL,
                "Responsibility" character varying(1000) NOT NULL,
                "Department" character varying(120) NULL,
                "DisplayOrder" integer NOT NULL,
                "IsActive" boolean NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_Staff" PRIMARY KEY ("Id")
            );
            """);

        await _context.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_Staff_DisplayOrder"
            ON "Staff" ("DisplayOrder");
            """);

        await _context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
            );
            """);

        await _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ('20260716094755_InitialCreate', '10.0.9')
            ON CONFLICT ("MigrationId") DO NOTHING;
            """);

        _logger.LogInformation("Legacy schema repair completed (Staff + migrations history).");
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    private async Task TrySeedAsync()
    {
        await SeedAdminUserAsync();
        await SeedStaffAsync();
    }

    private async Task SeedAdminUserAsync()
    {
        if (await _context.AdminUsers.AnyAsync())
        {
            return;
        }

        var seed = _configuration.GetSection(AdminSeedSettings.SectionName).Get<AdminSeedSettings>();
        if (seed is null ||
            string.IsNullOrWhiteSpace(seed.Email) ||
            string.IsNullOrWhiteSpace(seed.Password))
        {
            _logger.LogWarning("No admin user exists and AdminSeed is not configured. Skipping admin seed.");
            return;
        }

        var user = new AdminUser
        {
            Email = seed.Email.Trim().ToLowerInvariant(),
            FullName = seed.FullName.Trim(),
            Role = Roles.Admin,
            IsActive = true,
            PasswordHash = _passwordHasher.Hash(new AdminUser(), seed.Password),
        };

        _context.AdminUsers.Add(user);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded default admin user {Email}", user.Email);
    }

    private async Task SeedStaffAsync()
    {
        if (await _context.Staff.AnyAsync())
        {
            return;
        }

        var staff = new[]
        {
            new Staff
            {
                FullName = "Operations Lead",
                Role = "Field Operations",
                Responsibility = "Oversees manned guarding, patrol routes, and on-site incident response.",
                Department = "Operations",
                DisplayOrder = 1,
            },
            new Staff
            {
                FullName = "Client Success Manager",
                Role = "Account Management",
                Responsibility = "Main point of contact for reporting, scheduling, and service planning.",
                Department = "Client Services",
                DisplayOrder = 2,
            },
            new Staff
            {
                FullName = "Technical Supervisor",
                Role = "Systems & Monitoring",
                Responsibility = "Leads CCTV, access control, and alarm monitoring across client sites.",
                Department = "Technical",
                DisplayOrder = 3,
            },
        };

        _context.Staff.AddRange(staff);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded {Count} default staff members", staff.Length);
    }
}
