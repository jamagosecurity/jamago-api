using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyMail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MailAccountStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Folder = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UidValidity = table.Column<long>(type: "bigint", nullable: false),
                    LastUid = table.Column<long>(type: "bigint", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailAccountStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MailMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AccountAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Folder = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UidValidity = table.Column<long>(type: "bigint", nullable: false),
                    Uid = table.Column<long>(type: "bigint", nullable: false),
                    FromName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FromAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    ToAddresses = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Snippet = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    BodyText = table.Column<string>(type: "text", nullable: false),
                    BodyHtml = table.Column<string>(type: "text", nullable: true),
                    HasAttachments = table.Column<bool>(type: "boolean", nullable: false),
                    AttachmentNames = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MailAccountStates_AccountKey_Folder",
                table: "MailAccountStates",
                columns: new[] { "AccountKey", "Folder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MailMessages_AccountKey_Folder_UidValidity_Uid",
                table: "MailMessages",
                columns: new[] { "AccountKey", "Folder", "UidValidity", "Uid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MailMessages_AccountKey_SentAt",
                table: "MailMessages",
                columns: new[] { "AccountKey", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MailMessages_SentAt",
                table: "MailMessages",
                column: "SentAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MailAccountStates");

            migrationBuilder.DropTable(
                name: "MailMessages");
        }
    }
}
