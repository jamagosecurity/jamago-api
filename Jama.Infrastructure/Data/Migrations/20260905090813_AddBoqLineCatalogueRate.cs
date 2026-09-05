using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Records what the catalogue charged for a line, beside what the line was
    /// actually priced at, so an override is visible as a variance rather than
    /// replacing the list price.
    /// </summary>
    public partial class AddBoqLineCatalogueRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CatalogueRate",
                table: "BoqLines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Backfilled, because the default of 0 is not "no data" here — it is
            // a price, and the editor would read every existing line as given
            // away free. Nothing written before this migration could have been
            // overridden, since there was no way to do it: for all of them the
            // rate they carry IS the catalogue rate.
            migrationBuilder.Sql("""
                UPDATE "BoqLines" SET "CatalogueRate" = "UnitRate";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CatalogueRate",
                table: "BoqLines");
        }
    }
}
