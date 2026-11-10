using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class QuotationRevisionStartsAtZero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Revision used to start at 1. A quotation that was never amended is the first issue, which is now 0;
            // each amendment after approval added one, so subtract the amendments' worth: revision = number of Amended events.
            migrationBuilder.Sql("UPDATE \"Boqs\" b SET \"RevisionNumber\" = (SELECT COUNT(*) FROM \"BoqApprovalEvents\" e WHERE e.\"BoqId\" = b.\"Id\" AND e.\"Action\" = 'Amended') WHERE b.\"RevisionNumber\" = 1 OR b.\"RevisionNumber\" > 1;");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
