using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNonMoiJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NonMoiJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    DescriptionEn = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DescriptionAr = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AssignedToId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedToName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NonMoiJobs", x => x.Id);
                    table.CheckConstraint("CK_NonMoiJobs_SitePin", "(\"Latitude\" IS NULL) = (\"Longitude\" IS NULL) AND (\"Latitude\" IS NULL OR (\"Latitude\" BETWEEN -90 AND 90 AND \"Longitude\" BETWEEN -180 AND 180))");
                });

            migrationBuilder.CreateIndex(
                name: "IX_NonMoiJobs_AssignedToId",
                table: "NonMoiJobs",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_NonMoiJobs_JobNumber",
                table: "NonMoiJobs",
                column: "JobNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NonMoiJobs");
        }
    }
}
