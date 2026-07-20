using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTechnicianInspections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InspectionStartedDate",
                table: "DiaInspections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TechnicianInspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiaInspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quarter = table.Column<int>(type: "integer", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicianInspections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicianInspections_DiaInspections_DiaInspectionId",
                        column: x => x.DiaInspectionId,
                        principalTable: "DiaInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnprConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianInspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnprInstalled = table.Column<bool>(type: "boolean", nullable: false),
                    CameraDetails = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Configuration = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SoftwareVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnprConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnprConfigurations_TechnicianInspections_TechnicianInspecti~",
                        column: x => x.TechnicianInspectionId,
                        principalTable: "TechnicianInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CameraDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianInspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CameraDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CameraDetails_TechnicianInspections_TechnicianInspectionId",
                        column: x => x.TechnicianInspectionId,
                        principalTable: "TechnicianInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InspectionInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianInspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiaInspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quarter = table.Column<int>(type: "integer", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionInvoices_TechnicianInspections_TechnicianInspecti~",
                        column: x => x.TechnicianInspectionId,
                        principalTable: "TechnicianInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianInspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SwitchBrand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SwitchModel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RouterBrand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RouterModel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Firewall = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RackDetails = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NetworkRemarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkDetails_TechnicianInspections_TechnicianInspectionId",
                        column: x => x.TechnicianInspectionId,
                        principalTable: "TechnicianInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TechnicianInspectionHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianInspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiaInspectionId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_TechnicianInspectionHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicianInspectionHistory_TechnicianInspections_Technicia~",
                        column: x => x.TechnicianInspectionId,
                        principalTable: "TechnicianInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UpsGeneralDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianInspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpsBrand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpsCapacity = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    BatteryStatus = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    GeneratorAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    GeneratorDetails = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GeneralRemarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpsGeneralDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UpsGeneralDetails_TechnicianInspections_TechnicianInspectio~",
                        column: x => x.TechnicianInspectionId,
                        principalTable: "TechnicianInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VmsDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianInspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VmsName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LicenseDetails = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ServerDetails = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HealthStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VmsDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VmsDetails_TechnicianInspections_TechnicianInspectionId",
                        column: x => x.TechnicianInspectionId,
                        principalTable: "TechnicianInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnprConfigurations_TechnicianInspectionId",
                table: "AnprConfigurations",
                column: "TechnicianInspectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CameraDetails_TechnicianInspectionId",
                table: "CameraDetails",
                column: "TechnicianInspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionInvoices_DiaInspectionId_Quarter",
                table: "InspectionInvoices",
                columns: new[] { "DiaInspectionId", "Quarter" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionInvoices_InvoiceNumber",
                table: "InspectionInvoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionInvoices_TechnicianInspectionId",
                table: "InspectionInvoices",
                column: "TechnicianInspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkDetails_TechnicianInspectionId",
                table: "NetworkDetails",
                column: "TechnicianInspectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianInspectionHistory_DiaInspectionId_CreatedAt",
                table: "TechnicianInspectionHistory",
                columns: new[] { "DiaInspectionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianInspectionHistory_TechnicianInspectionId_CreatedAt",
                table: "TechnicianInspectionHistory",
                columns: new[] { "TechnicianInspectionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianInspections_DiaInspectionId_Quarter_IsDeleted",
                table: "TechnicianInspections",
                columns: new[] { "DiaInspectionId", "Quarter", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianInspections_TechnicianId_Status",
                table: "TechnicianInspections",
                columns: new[] { "TechnicianId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UpsGeneralDetails_TechnicianInspectionId",
                table: "UpsGeneralDetails",
                column: "TechnicianInspectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VmsDetails_TechnicianInspectionId",
                table: "VmsDetails",
                column: "TechnicianInspectionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnprConfigurations");

            migrationBuilder.DropTable(
                name: "CameraDetails");

            migrationBuilder.DropTable(
                name: "InspectionInvoices");

            migrationBuilder.DropTable(
                name: "NetworkDetails");

            migrationBuilder.DropTable(
                name: "TechnicianInspectionHistory");

            migrationBuilder.DropTable(
                name: "UpsGeneralDetails");

            migrationBuilder.DropTable(
                name: "VmsDetails");

            migrationBuilder.DropTable(
                name: "TechnicianInspections");

            migrationBuilder.DropColumn(
                name: "InspectionStartedDate",
                table: "DiaInspections");
        }
    }
}
