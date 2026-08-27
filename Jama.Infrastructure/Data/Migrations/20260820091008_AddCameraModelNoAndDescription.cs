using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCameraModelNoAndDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cameras_Brand_Type",
                table: "Cameras");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Cameras",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelNo",
                table: "Cameras",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Cameras_Brand_Type_ModelNo",
                table: "Cameras",
                columns: new[] { "Brand", "Type", "ModelNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cameras_Brand_Type_ModelNo",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "ModelNo",
                table: "Cameras");

            migrationBuilder.CreateIndex(
                name: "IX_Cameras_Brand_Type",
                table: "Cameras",
                columns: new[] { "Brand", "Type" },
                unique: true);
        }
    }
}
