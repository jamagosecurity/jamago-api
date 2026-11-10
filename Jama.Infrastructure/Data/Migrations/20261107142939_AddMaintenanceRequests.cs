using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceCatalogueItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceCatalogueItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SiteLocation = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    ClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Discount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ServiceFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ReportedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportedByName = table.Column<string>(type: "text", nullable: true),
                    AssignedToId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedToName = table.Column<string>(type: "text", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResolvedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedByName = table.Column<string>(type: "text", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceRequestLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaintenanceRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IssueType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitFee = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    CatalogueFee = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceRequestLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceRequestLines_MaintenanceRequests_MaintenanceRequ~",
                        column: x => x.MaintenanceRequestId,
                        principalTable: "MaintenanceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceCatalogueItems_Category_Name",
                table: "MaintenanceCatalogueItems",
                columns: new[] { "Category", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequestLines_Category",
                table: "MaintenanceRequestLines",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequestLines_MaintenanceRequestId_SortOrder",
                table: "MaintenanceRequestLines",
                columns: new[] { "MaintenanceRequestId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_AssignedToId",
                table: "MaintenanceRequests",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_ReportedById",
                table: "MaintenanceRequests",
                column: "ReportedById");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_RequestNumber",
                table: "MaintenanceRequests",
                column: "RequestNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_Status",
                table: "MaintenanceRequests",
                column: "Status");

            // The Non-MOI price list admin starts from — the services and
            // prices that were fixed in code before admin could manage them.
            // Inserted once, here, rather than via HasData: admin owns these
            // rows from now on, and HasData would let a later migration
            // overwrite what admin has changed.
            var seededAt = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);
            migrationBuilder.InsertData(
                table: "MaintenanceCatalogueItems",
                columns: ["Id", "Category", "Name", "Price", "SortOrder", "CreatedAt"],
                values: new object[,]
                {
                    { new Guid("b18f2208-a887-4f30-8ef9-eadbff8f1a05"), "PowerService", "Power adapter replacement", 250m, 0, seededAt },
                    { new Guid("69e479eb-6495-40b8-b266-ec7e01de4787"), "PowerService", "PDU replacement", 250m, 1, seededAt },
                    { new Guid("f57e329f-4482-4119-9bc8-020dd80a19a1"), "PowerService", "NVR power issue", 250m, 2, seededAt },
                    { new Guid("38201764-b29b-400f-b201-c66ad0f1e3c9"), "PowerService", "Camera power interrupted", 250m, 3, seededAt },
                    { new Guid("c1399616-4c2d-4d1a-b66c-7a2f45fd5b2f"), "Connectivity", "Connector/BNC replacement/fixing", 250m, 0, seededAt },
                    { new Guid("a9abf4a8-6ac1-40d4-be20-5b865d66217b"), "Connectivity", "Cable changing", 250m, 1, seededAt },
                    { new Guid("9d798b4c-7727-4e2e-a178-8550d75c9142"), "Connectivity", "Cable cut", 250m, 2, seededAt },
                    { new Guid("9994cce0-dfa6-4929-a04b-6f831e126f11"), "Camera", "Camera shift and relocation", 250m, 0, seededAt },
                    { new Guid("63a6b164-14e0-421e-923e-cd16c1a50e20"), "Camera", "Camera replacement", 500m, 1, seededAt },
                    { new Guid("88a35e90-5ca3-40c3-bdb3-2acc29cc298a"), "Camera", "Camera confusion/reset", 250m, 2, seededAt },
                    { new Guid("03ffffc0-0edf-410a-ba02-a2cfd078693e"), "Camera", "Camera removal/installation", 350m, 3, seededAt },
                    { new Guid("8d583e2b-9684-4063-ae6a-b4227d019e96"), "NvrStorage", "NVR power issue", 250m, 0, seededAt },
                    { new Guid("d84a8450-c3f7-4b0f-a9fa-69fe90920255"), "NvrStorage", "NVR replacement", 250m, 1, seededAt },
                    { new Guid("90b7d330-804a-4ac7-9a57-8b2faad2ea2a"), "NvrStorage", "Hard disk replacement", 250m, 2, seededAt },
                    { new Guid("26e8c458-2ebf-41b5-a678-ef73286ea663"), "NvrStorage", "NVR shifting", 250m, 3, seededAt },
                    { new Guid("5b541bef-133c-4dff-8117-c3b5d1c4d4f8"), "Software", "Password reset due to loss/forgot", 250m, 0, seededAt },
                    { new Guid("489c1155-62a7-4c68-a372-814882835fe6"), "Software", "Video playback request", 250m, 1, seededAt },
                    { new Guid("a16aafd1-bd5c-45b3-9c96-374cc974d79e"), "Software", "Video loss in monitor/mobile", 250m, 2, seededAt },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceCatalogueItems");

            migrationBuilder.DropTable(
                name: "MaintenanceRequestLines");

            migrationBuilder.DropTable(
                name: "MaintenanceRequests");
        }
    }
}
