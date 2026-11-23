using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTravelClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "ReimbursementNumbers");

            migrationBuilder.CreateSequence(
                name: "TravelClaimNumbers");

            migrationBuilder.CreateTable(
                name: "LunchRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountPerPerson = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    SetById = table.Column<Guid>(type: "uuid", nullable: false),
                    SetByName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LunchRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reimbursements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PayeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayeeName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidById = table.Column<Guid>(type: "uuid", nullable: false),
                    PaidByName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reimbursements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TravelClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ClaimantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimantName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    TripOn = table.Column<DateOnly>(type: "date", nullable: false),
                    TripTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    TripType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SiteName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Pickup = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Dropoff = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ClaimantAteLunch = table.Column<bool>(type: "boolean", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ApprovedAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    AdjustmentReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedByName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedById = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectedByName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RejectReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WithdrawnAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReimbursementId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TravelClaims_Reimbursements_ReimbursementId",
                        column: x => x.ReimbursementId,
                        principalTable: "Reimbursements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TravelClaimEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TravelClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelClaimEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TravelClaimEvents_TravelClaims_TravelClaimId",
                        column: x => x.TravelClaimId,
                        principalTable: "TravelClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TravelClaimLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TravelClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BillAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Persons = table.Column<int>(type: "integer", nullable: false),
                    RatePerPerson = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    ReadFromReceipt = table.Column<bool>(type: "boolean", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelClaimLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TravelClaimLines_TravelClaims_TravelClaimId",
                        column: x => x.TravelClaimId,
                        principalTable: "TravelClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TravelClaimPeople",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TravelClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    AteLunch = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelClaimPeople", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TravelClaimPeople_TravelClaims_TravelClaimId",
                        column: x => x.TravelClaimId,
                        principalTable: "TravelClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LunchRates_EffectiveFrom",
                table: "LunchRates",
                column: "EffectiveFrom");

            migrationBuilder.CreateIndex(
                name: "IX_Reimbursements_Number",
                table: "Reimbursements",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reimbursements_PayeeId_PaidAt",
                table: "Reimbursements",
                columns: new[] { "PayeeId", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TravelClaimEvents_TravelClaimId_CreatedAt",
                table: "TravelClaimEvents",
                columns: new[] { "TravelClaimId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TravelClaimLines_FileHash",
                table: "TravelClaimLines",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_TravelClaimLines_TravelClaimId",
                table: "TravelClaimLines",
                column: "TravelClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_TravelClaimPeople_PersonId",
                table: "TravelClaimPeople",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_TravelClaimPeople_TravelClaimId",
                table: "TravelClaimPeople",
                column: "TravelClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_TravelClaims_ClaimantId_Status",
                table: "TravelClaims",
                columns: new[] { "ClaimantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TravelClaims_ClaimNumber",
                table: "TravelClaims",
                column: "ClaimNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TravelClaims_ReimbursementId",
                table: "TravelClaims",
                column: "ReimbursementId");

            migrationBuilder.CreateIndex(
                name: "IX_TravelClaims_Status_SubmittedAt",
                table: "TravelClaims",
                columns: new[] { "Status", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TravelClaims_TripOn",
                table: "TravelClaims",
                column: "TripOn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LunchRates");

            migrationBuilder.DropTable(
                name: "TravelClaimEvents");

            migrationBuilder.DropTable(
                name: "TravelClaimLines");

            migrationBuilder.DropTable(
                name: "TravelClaimPeople");

            migrationBuilder.DropTable(
                name: "TravelClaims");

            migrationBuilder.DropTable(
                name: "Reimbursements");

            migrationBuilder.DropSequence(
                name: "ReimbursementNumbers");

            migrationBuilder.DropSequence(
                name: "TravelClaimNumbers");
        }
    }
}
