using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiaSiteCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "DiaInspections",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "DiaInspections",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_DiaInspections_SitePin",
                table: "DiaInspections",
                sql: "(\"Latitude\" IS NULL) = (\"Longitude\" IS NULL) AND (\"Latitude\" IS NULL OR (\"Latitude\" BETWEEN -90 AND 90 AND \"Longitude\" BETWEEN -180 AND 180))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DiaInspections_SitePin",
                table: "DiaInspections");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "DiaInspections");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "DiaInspections");
        }
    }
}
