using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBoqSpecialDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GrandTotal",
                table: "Boqs",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SpecialDiscount",
                table: "Boqs",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Every quotation written before today has no discount, so what is
            // payable on it is what its lines came to. Left at the column
            // default, each of them would print and list a final amount of zero
            // until somebody happened to re-save it.
            migrationBuilder.Sql(@"UPDATE ""Boqs"" SET ""GrandTotal"" = ""Total"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GrandTotal",
                table: "Boqs");

            migrationBuilder.DropColumn(
                name: "SpecialDiscount",
                table: "Boqs");
        }
    }
}
