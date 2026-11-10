using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceDescriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescriptionAr",
                table: "MaintenanceRequestLines",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionEn",
                table: "MaintenanceRequestLines",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionAr",
                table: "MaintenanceCatalogueItems",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionEn",
                table: "MaintenanceCatalogueItems",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescriptionAr",
                table: "MaintenanceRequestLines");

            migrationBuilder.DropColumn(
                name: "DescriptionEn",
                table: "MaintenanceRequestLines");

            migrationBuilder.DropColumn(
                name: "DescriptionAr",
                table: "MaintenanceCatalogueItems");

            migrationBuilder.DropColumn(
                name: "DescriptionEn",
                table: "MaintenanceCatalogueItems");
        }
    }
}
