using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMoiJobsAndServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MoiJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    DescriptionEn = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DescriptionAr = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssignedToId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedToName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CustomerEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    AssignedById = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedByName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, defaultValue: ""),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Assigned"),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WorkTotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CompletionNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Discount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    AmountPayable = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedByName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    EmailSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WhatsAppSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentFromAccount = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SentById = table.Column<Guid>(type: "uuid", nullable: true),
                    BillToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    BillRevision = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ClientDecision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ClientMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ClientResponderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ClientRespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoiJobs", x => x.Id);
                    table.CheckConstraint("CK_MoiJobs_SitePin", "(\"Latitude\" IS NULL) = (\"Longitude\" IS NULL) AND (\"Latitude\" IS NULL OR (\"Latitude\" BETWEEN -90 AND 90 AND \"Longitude\" BETWEEN -180 AND 180))");
                });

            migrationBuilder.CreateTable(
                name: "MoiServiceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DescriptionEn = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DescriptionAr = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoiServiceItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MoiJobEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MoiJobId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_MoiJobEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MoiJobEvents_MoiJobs_MoiJobId",
                        column: x => x.MoiJobId,
                        principalTable: "MoiJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MoiJobLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MoiJobId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_MoiJobLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MoiJobLines_MoiJobs_MoiJobId",
                        column: x => x.MoiJobId,
                        principalTable: "MoiJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MoiJobPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MoiJobId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_MoiJobPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MoiJobPayments_MoiJobs_MoiJobId",
                        column: x => x.MoiJobId,
                        principalTable: "MoiJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MoiServiceImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogueItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoiServiceImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MoiServiceImages_MoiServiceItems_CatalogueItemId",
                        column: x => x.CatalogueItemId,
                        principalTable: "MoiServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MoiJobEvents_MoiJobId",
                table: "MoiJobEvents",
                column: "MoiJobId");

            migrationBuilder.CreateIndex(
                name: "IX_MoiJobLines_MoiJobId",
                table: "MoiJobLines",
                column: "MoiJobId");

            migrationBuilder.CreateIndex(
                name: "IX_MoiJobPayments_MoiJobId",
                table: "MoiJobPayments",
                column: "MoiJobId");

            migrationBuilder.CreateIndex(
                name: "IX_MoiJobPayments_ReceivedOn",
                table: "MoiJobPayments",
                column: "ReceivedOn");

            migrationBuilder.CreateIndex(
                name: "IX_MoiJobs_AssignedToId",
                table: "MoiJobs",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_MoiJobs_BillToken",
                table: "MoiJobs",
                column: "BillToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MoiJobs_JobNumber",
                table: "MoiJobs",
                column: "JobNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MoiServiceImages_CatalogueItemId_SortOrder",
                table: "MoiServiceImages",
                columns: new[] { "CatalogueItemId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MoiServiceItems_Category_Name",
                table: "MoiServiceItems",
                columns: new[] { "Category", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MoiJobEvents");

            migrationBuilder.DropTable(
                name: "MoiJobLines");

            migrationBuilder.DropTable(
                name: "MoiJobPayments");

            migrationBuilder.DropTable(
                name: "MoiServiceImages");

            migrationBuilder.DropTable(
                name: "MoiJobs");

            migrationBuilder.DropTable(
                name: "MoiServiceItems");
        }
    }
}
