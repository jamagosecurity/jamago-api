using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Jama.Application.Tests;

public sealed class DiaMigrationTests
{
    [Fact]
    public void PostgreSql_migration_script_contains_required_dia_schema()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=jamago_test;Username=test;Password=test")
            .Options;
        using var context = new ApplicationDbContext(options);

        var script = context.GetService<IMigrator>().GenerateScript(
            options: MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains("DiaInspections", script);
        Assert.Contains("DiaInspectionHistory", script);
        Assert.Contains("jsonb", script);
        Assert.Contains("IX_DiaInspections_NormalizedDiaNumber", script);
        Assert.Contains("CREATE UNIQUE INDEX", script);
    }
}
