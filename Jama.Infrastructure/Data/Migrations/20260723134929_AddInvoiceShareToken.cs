using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceShareToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShareToken",
                table: "InspectionInvoices",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShareTokenExpiresAtUtc",
                table: "InspectionInvoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionInvoices_ShareToken",
                table: "InspectionInvoices",
                column: "ShareToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InspectionInvoices_ShareToken",
                table: "InspectionInvoices");

            migrationBuilder.DropColumn(
                name: "ShareToken",
                table: "InspectionInvoices");

            migrationBuilder.DropColumn(
                name: "ShareTokenExpiresAtUtc",
                table: "InspectionInvoices");
        }
    }
}
