using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Pack and Pair leave the unit pick-list.
    ///
    /// No schema change: Uom is a string column and the enum members were never
    /// part of it. What does change is which words can be read back — a row
    /// still saying "Pack" cannot materialise into the enum any more, and would
    /// throw on the next query that touched it. So the rows move rather than the
    /// column.
    ///
    /// Each goes to its nearest surviving unit: a pack is a container, so Box; a
    /// pair is two of a thing, so Piece. Neither was ever used on a quoted
    /// document, so this is expected to touch nothing — it exists so that a
    /// database where somebody did use one does not start failing to read.
    /// </summary>
    public partial class RetireUnusedUnitsOfMeasurement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Cameras" SET "Uom" = 'Box'   WHERE "Uom" = 'Pack';
                UPDATE "Cameras" SET "Uom" = 'Piece' WHERE "Uom" = 'Pair';
                UPDATE "BoqLines" SET "Uom" = 'Box'   WHERE "Uom" = 'Pack';
                UPDATE "BoqLines" SET "Uom" = 'Piece' WHERE "Uom" = 'Pair';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately empty. The original values are not recoverable — a row
            // reading "Box" here may always have been a Box — and guessing which
            // ones to turn back would corrupt the rows that were never touched.
        }
    }
}
