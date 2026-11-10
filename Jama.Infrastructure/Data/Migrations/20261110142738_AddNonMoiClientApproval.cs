using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNonMoiClientApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BillRevision",
                table: "NonMoiJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ClientDecision",
                table: "NonMoiJobs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientMessage",
                table: "NonMoiJobs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClientRespondedAt",
                table: "NonMoiJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientResponderName",
                table: "NonMoiJobs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NonMoiJobEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NonMoiJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActorName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    AmountPayable = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NonMoiJobEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NonMoiJobEvents_NonMoiJobs_NonMoiJobId",
                        column: x => x.NonMoiJobId,
                        principalTable: "NonMoiJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NonMoiJobEvents_NonMoiJobId",
                table: "NonMoiJobEvents",
                column: "NonMoiJobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NonMoiJobEvents");

            migrationBuilder.DropColumn(
                name: "BillRevision",
                table: "NonMoiJobs");

            migrationBuilder.DropColumn(
                name: "ClientDecision",
                table: "NonMoiJobs");

            migrationBuilder.DropColumn(
                name: "ClientMessage",
                table: "NonMoiJobs");

            migrationBuilder.DropColumn(
                name: "ClientRespondedAt",
                table: "NonMoiJobs");

            migrationBuilder.DropColumn(
                name: "ClientResponderName",
                table: "NonMoiJobs");
        }
    }
}
