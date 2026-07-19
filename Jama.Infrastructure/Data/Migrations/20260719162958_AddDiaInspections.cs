using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiaInspections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiaInspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiaNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedDiaNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClientNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ClientLocation = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ActivatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiaInspections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiaInspectionHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiaInspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BeforeJson = table.Column<string>(type: "jsonb", nullable: true),
                    AfterJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiaInspectionHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiaInspectionHistory_DiaInspections_DiaInspectionId",
                        column: x => x.DiaInspectionId,
                        principalTable: "DiaInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiaInspectionHistory_Action_CreatedAt",
                table: "DiaInspectionHistory",
                columns: new[] { "Action", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DiaInspectionHistory_DiaInspectionId_CreatedAt",
                table: "DiaInspectionHistory",
                columns: new[] { "DiaInspectionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DiaInspections_ActivatedDate",
                table: "DiaInspections",
                column: "ActivatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_DiaInspections_IsArchived_IsActive",
                table: "DiaInspections",
                columns: new[] { "IsArchived", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_DiaInspections_NormalizedDiaNumber",
                table: "DiaInspections",
                column: "NormalizedDiaNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiaInspectionHistory");

            migrationBuilder.DropTable(
                name: "DiaInspections");
        }
    }
}
