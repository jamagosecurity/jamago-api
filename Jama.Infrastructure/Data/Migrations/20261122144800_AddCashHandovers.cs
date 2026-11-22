using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCashHandovers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CashHandoverId",
                table: "NonMoiJobPayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CashHandoverId",
                table: "MoiJobPayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CashHandovers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CollectorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CollectorName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ReceivedAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceivedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedByName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashHandovers", x => x.Id);
                    table.CheckConstraint("CK_CashHandovers_Kind", "\"Kind\" IN ('MOI', 'NonMOI')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_NonMoiJobPayments_CashHandoverId",
                table: "NonMoiJobPayments",
                column: "CashHandoverId");

            migrationBuilder.CreateIndex(
                name: "IX_MoiJobPayments_CashHandoverId",
                table: "MoiJobPayments",
                column: "CashHandoverId");

            migrationBuilder.CreateIndex(
                name: "IX_CashHandovers_CreatedAt",
                table: "CashHandovers",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CashHandovers_Kind_CollectorId",
                table: "CashHandovers",
                columns: new[] { "Kind", "CollectorId" });

            migrationBuilder.AddForeignKey(
                name: "FK_MoiJobPayments_CashHandovers_CashHandoverId",
                table: "MoiJobPayments",
                column: "CashHandoverId",
                principalTable: "CashHandovers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NonMoiJobPayments_CashHandovers_CashHandoverId",
                table: "NonMoiJobPayments",
                column: "CashHandoverId",
                principalTable: "CashHandovers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MoiJobPayments_CashHandovers_CashHandoverId",
                table: "MoiJobPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_NonMoiJobPayments_CashHandovers_CashHandoverId",
                table: "NonMoiJobPayments");

            migrationBuilder.DropTable(
                name: "CashHandovers");

            migrationBuilder.DropIndex(
                name: "IX_NonMoiJobPayments_CashHandoverId",
                table: "NonMoiJobPayments");

            migrationBuilder.DropIndex(
                name: "IX_MoiJobPayments_CashHandoverId",
                table: "MoiJobPayments");

            migrationBuilder.DropColumn(
                name: "CashHandoverId",
                table: "NonMoiJobPayments");

            migrationBuilder.DropColumn(
                name: "CashHandoverId",
                table: "MoiJobPayments");
        }
    }
}
