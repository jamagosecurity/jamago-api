using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNonMoiJobPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NonMoiJobPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NonMoiJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CollectedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CollectedByName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceiptFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    ReceiptStorageKey = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    ReceiptContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RecordedById = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedByName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NonMoiJobPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NonMoiJobPayments_NonMoiJobs_NonMoiJobId",
                        column: x => x.NonMoiJobId,
                        principalTable: "NonMoiJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NonMoiJobPayments_NonMoiJobId",
                table: "NonMoiJobPayments",
                column: "NonMoiJobId");

            migrationBuilder.CreateIndex(
                name: "IX_NonMoiJobPayments_ReceivedOn",
                table: "NonMoiJobPayments",
                column: "ReceivedOn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NonMoiJobPayments");
        }
    }
}
