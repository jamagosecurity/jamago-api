using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <summary>
    /// The unit priced as one lump is called a Lot, which is what the trade calls
    /// it and what a customer reads on the quotation. It was named Location.
    ///
    /// UnitOfMeasurement is stored as the enum NAME rather than an ordinal — see
    /// the HasConversion&lt;string&gt; on Cameras and BoqLines — so renaming the
    /// member renames the stored value with it. Without this, every row still
    /// holding "Location" fails to parse on read and takes the stock list and any
    /// quotation containing one down with it.
    ///
    /// Data only: no column changes, which is why the scaffolded migration was
    /// empty.
    /// </summary>
    public partial class RenameLocationUnitToLot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Both tables that store the unit. Migrations run before the app
            // serves its first request, so nothing reads a stale value.
            migrationBuilder.Sql(@"UPDATE ""Cameras"" SET ""Uom"" = 'Lot' WHERE ""Uom"" = 'Location';");
            migrationBuilder.Sql(@"UPDATE ""BoqLines"" SET ""Uom"" = 'Lot' WHERE ""Uom"" = 'Location';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ""Cameras"" SET ""Uom"" = 'Location' WHERE ""Uom"" = 'Lot';");
            migrationBuilder.Sql(@"UPDATE ""BoqLines"" SET ""Uom"" = 'Location' WHERE ""Uom"" = 'Lot';");
        }
    }
}
