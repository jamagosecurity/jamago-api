using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <summary>
    /// The lump-sum discount given on a finished quotation, in QAR.
    ///
    /// Defaults to zero, which leaves every quotation already issued exactly as
    /// it was: GrandTotal now means the amount payable after this discount, and
    /// with nothing discounted that is the figure those rows already hold.
    /// </summary>
    public partial class AddQuotationSpecialDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SpecialDiscount",
                table: "Quotations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpecialDiscount",
                table: "Quotations");
        }
    }
}
