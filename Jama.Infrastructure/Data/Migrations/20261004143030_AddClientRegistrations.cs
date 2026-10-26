using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientRegistrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Mobile = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SiteLocation = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Teams = table.Column<List<string>>(type: "text[]", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiaInspectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedByName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientRegistrations_AdminUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientRegistrations_DiaInspections_DiaInspectionId",
                        column: x => x.DiaInspectionId,
                        principalTable: "DiaInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientRegistrations_AdminUserId",
                table: "ClientRegistrations",
                column: "AdminUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientRegistrations_DiaInspectionId",
                table: "ClientRegistrations",
                column: "DiaInspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientRegistrations_Status_CreatedAt",
                table: "ClientRegistrations",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientRegistrations");
        }
    }
}
