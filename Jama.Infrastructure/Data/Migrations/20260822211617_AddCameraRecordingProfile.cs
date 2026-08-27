using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCameraRecordingProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BitrateMbps",
                table: "Cameras",
                type: "numeric(8,3)",
                precision: 8,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Resolution",
                table: "Cameras",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                // NOT "" as the scaffolder emits: the column stores the enum NAME,
                // and an empty string cannot be materialised back into
                // CameraResolution, so every existing row would fail to read.
                defaultValue: "Unspecified");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BitrateMbps",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "Resolution",
                table: "Cameras");
        }
    }
}
