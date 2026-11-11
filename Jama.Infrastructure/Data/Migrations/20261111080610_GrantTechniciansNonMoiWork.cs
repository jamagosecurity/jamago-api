using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class GrantTechniciansNonMoiWork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The technician portal now shows the Non-MOI section only to those granted "nonmoijob.work". A technician with NO explicit
            // grants gets it from the role baseline. One with explicit grants who already has jobs issued to them must not lose them:
            // grant it to exactly those.
            migrationBuilder.Sql("""
                INSERT INTO "UserPermissions" ("Id", "AdminUserId", "Permission", "CreatedAt")
                SELECT gen_random_uuid(), a."Id", 'nonmoijob.work', now()
                FROM "AdminUsers" a
                WHERE a."Role" = 'Technician'
                  AND EXISTS (SELECT 1 FROM "UserPermissions" up WHERE up."AdminUserId" = a."Id")
                  AND NOT EXISTS (SELECT 1 FROM "UserPermissions" up WHERE up."AdminUserId" = a."Id" AND up."Permission" = 'nonmoijob.work')
                  AND EXISTS (SELECT 1 FROM "NonMoiJobs" j WHERE j."AssignedToId" = a."Id");
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
