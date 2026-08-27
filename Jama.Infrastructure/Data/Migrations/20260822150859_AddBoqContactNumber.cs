using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBoqContactNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactNumber",
                table: "Boqs",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactNumber",
                table: "Boqs");
        }
    }
}
