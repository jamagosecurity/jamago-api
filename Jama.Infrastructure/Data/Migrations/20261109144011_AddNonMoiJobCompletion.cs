using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNonMoiJobCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "NonMoiJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletionNotes",
                table: "NonMoiJobs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "NonMoiJobs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Assigned");

            migrationBuilder.AddColumn<decimal>(
                name: "WorkTotal",
                table: "NonMoiJobs",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "NonMoiJobLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NonMoiJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IssueType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NonMoiJobLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NonMoiJobLines_NonMoiJobs_NonMoiJobId",
                        column: x => x.NonMoiJobId,
                        principalTable: "NonMoiJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NonMoiJobLines_NonMoiJobId",
                table: "NonMoiJobLines",
                column: "NonMoiJobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NonMoiJobLines");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "NonMoiJobs");

            migrationBuilder.DropColumn(
                name: "CompletionNotes",
                table: "NonMoiJobs");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "NonMoiJobs");

            migrationBuilder.DropColumn(
                name: "WorkTotal",
                table: "NonMoiJobs");
        }
    }
}
