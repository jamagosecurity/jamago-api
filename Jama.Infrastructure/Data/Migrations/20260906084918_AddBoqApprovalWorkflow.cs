using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBoqApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Boqs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedById",
                table: "Boqs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByName",
                table: "Boqs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "Boqs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedById",
                table: "Boqs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedByName",
                table: "Boqs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Boqs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "Boqs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BoqApprovalEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoqId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoqApprovalEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoqApprovalEvents_Boqs_BoqId",
                        column: x => x.BoqId,
                        principalTable: "Boqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Boqs_Status_CreatedAt",
                table: "Boqs",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BoqApprovalEvents_BoqId_CreatedAt",
                table: "BoqApprovalEvents",
                columns: new[] { "BoqId", "CreatedAt" });

            // Every quotation that already exists gets the one step we can state
            // truthfully: it was created, by whoever is recorded as preparing it,
            // when the row says. Without this their history opens empty, which
            // reads as "nothing is recorded about this document" rather than
            // "this document predates the trail".
            //
            // Nothing is invented for the ones already sitting at Submitted,
            // Approved or Rejected. Who decided and when was never captured, and
            // a history that guesses is worse than one that is short.
            migrationBuilder.Sql(@"
                INSERT INTO ""BoqApprovalEvents""
                    (""Id"", ""BoqId"", ""Action"", ""ActorId"", ""ActorName"", ""Reason"", ""CreatedAt"")
                SELECT gen_random_uuid(), b.""Id"", 'Created', b.""PreparedById"", b.""PreparedByName"", NULL, b.""CreatedAt""
                FROM ""Boqs"" b;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoqApprovalEvents");

            migrationBuilder.DropIndex(
                name: "IX_Boqs_Status_CreatedAt",
                table: "Boqs");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Boqs");

            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "Boqs");

            migrationBuilder.DropColumn(
                name: "ApprovedByName",
                table: "Boqs");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "Boqs");

            migrationBuilder.DropColumn(
                name: "RejectedById",
                table: "Boqs");

            migrationBuilder.DropColumn(
                name: "RejectedByName",
                table: "Boqs");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Boqs");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "Boqs");
        }
    }
}
