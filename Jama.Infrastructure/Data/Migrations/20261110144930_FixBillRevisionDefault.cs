using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixBillRevisionDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Jobs that existed before the approval feature were given 0 by the column being added; the first issue of a bill is revision 1.
            migrationBuilder.Sql("UPDATE \"NonMoiJobs\" SET \"BillRevision\" = 1 WHERE \"BillRevision\" < 1;");
            migrationBuilder.AlterColumn<int>(
                name: "BillRevision",
                table: "NonMoiJobs",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "BillRevision",
                table: "NonMoiJobs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);
        }
    }
}
