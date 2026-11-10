using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNonMoiJobClosing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountPayable",
                table: "NonMoiJobs",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedById",
                table: "NonMoiJobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "AssignedByName",
                table: "NonMoiJobs",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BillToken",
                table: "NonMoiJobs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "NonMoiJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClosedById",
                table: "NonMoiJobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosedByName",
                table: "NonMoiJobs",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerEmail",
                table: "NonMoiJobs",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                table: "NonMoiJobs",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailSentAt",
                table: "NonMoiJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SentFromAccount",
                table: "NonMoiJobs",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WhatsAppSentAt",
                table: "NonMoiJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NonMoiJobs_BillToken",
                table: "NonMoiJobs",
                column: "BillToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NonMoiJobs_BillToken",
                table: "NonMoiJobs");

            migrationBuilder.DropColumn(
                name: "AmountPayable",
                table: "NonMoiJobs");

            migrationBuilder.DropColumn(
                name: "AssignedById",
                table: "NonMoiJobs");

            migrationBuilder.DropColumn(
                name: "AssignedByName",
                table: "NonMoiJobs");

            migrationBuilder.DropColumn(
                name: "BillToken",
                table: "NonMoiJobs");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "NonMoiJobs");

            migrationBuilder.DropColumn(
                name: "ClosedById",
                table: "NonMoiJobs");

            migrationBuilder.DropColumn(
                name: "ClosedByName",
                table: "NonMoiJobs");

            migrationBuilder.DropColumn(
                name: "CustomerEmail",
                table: "NonMoiJobs");

            migrationBuilder.DropColumn(
                name: "Discount",
                table: "NonMoiJobs");

            migrationBuilder.DropColumn(
                name: "EmailSentAt",
                table: "NonMoiJobs");

            migrationBuilder.DropColumn(
                name: "SentFromAccount",
                table: "NonMoiJobs");

            migrationBuilder.DropColumn(
                name: "WhatsAppSentAt",
                table: "NonMoiJobs");
        }
    }
}
