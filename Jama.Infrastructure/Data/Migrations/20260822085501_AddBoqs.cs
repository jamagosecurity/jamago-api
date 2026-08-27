using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBoqs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Boqs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoqNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProjectName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SiteLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PreparedById = table.Column<Guid>(type: "uuid", nullable: false),
                    PreparedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boqs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BoqSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoqId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoqSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoqSections_Boqs_BoqId",
                        column: x => x.BoqId,
                        principalTable: "Boqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoqLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoqSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CameraId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ModelNo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Uom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    UnitRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoqLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoqLines_BoqSections_BoqSectionId",
                        column: x => x.BoqSectionId,
                        principalTable: "BoqSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoqLines_Cameras_CameraId",
                        column: x => x.CameraId,
                        principalTable: "Cameras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoqLines_BoqSectionId_SortOrder",
                table: "BoqLines",
                columns: new[] { "BoqSectionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_BoqLines_CameraId",
                table: "BoqLines",
                column: "CameraId");

            migrationBuilder.CreateIndex(
                name: "IX_Boqs_BoqNumber",
                table: "Boqs",
                column: "BoqNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Boqs_PreparedById",
                table: "Boqs",
                column: "PreparedById");

            migrationBuilder.CreateIndex(
                name: "IX_BoqSections_BoqId_SortOrder",
                table: "BoqSections",
                columns: new[] { "BoqId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoqLines");

            migrationBuilder.DropTable(
                name: "BoqSections");

            migrationBuilder.DropTable(
                name: "Boqs");
        }
    }
}
