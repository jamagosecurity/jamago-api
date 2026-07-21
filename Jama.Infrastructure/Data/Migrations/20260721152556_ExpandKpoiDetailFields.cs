using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExpandKpoiDetailFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Details",
                table: "KpoiDetails");

            migrationBuilder.AddColumn<string>(
                name: "HardDisc",
                table: "KpoiDetails",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IvdIvss",
                table: "KpoiDetails",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KpoiCamera",
                table: "KpoiDetails",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Lens",
                table: "KpoiDetails",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HardDisc",
                table: "KpoiDetails");

            migrationBuilder.DropColumn(
                name: "IvdIvss",
                table: "KpoiDetails");

            migrationBuilder.DropColumn(
                name: "KpoiCamera",
                table: "KpoiDetails");

            migrationBuilder.DropColumn(
                name: "Lens",
                table: "KpoiDetails");

            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "KpoiDetails",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }
    }
}
