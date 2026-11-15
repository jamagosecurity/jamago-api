using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMailSourceKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MailMessages_AccountKey_Folder_UidValidity_Uid",
                table: "MailMessages");

            migrationBuilder.AddColumn<string>(
                name: "SourceKey",
                table: "MailMessages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Everything copied so far was read from its own mailbox.
            migrationBuilder.Sql("UPDATE \"MailMessages\" SET \"SourceKey\" = \"AccountKey\" WHERE \"SourceKey\" = '';");

            migrationBuilder.CreateIndex(
                name: "IX_MailMessages_AccountKey_Folder_MessageId",
                table: "MailMessages",
                columns: new[] { "AccountKey", "Folder", "MessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_MailMessages_SourceKey_Folder_UidValidity_Uid",
                table: "MailMessages",
                columns: new[] { "SourceKey", "Folder", "UidValidity", "Uid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MailMessages_AccountKey_Folder_MessageId",
                table: "MailMessages");

            migrationBuilder.DropIndex(
                name: "IX_MailMessages_SourceKey_Folder_UidValidity_Uid",
                table: "MailMessages");

            migrationBuilder.DropColumn(
                name: "SourceKey",
                table: "MailMessages");

            migrationBuilder.CreateIndex(
                name: "IX_MailMessages_AccountKey_Folder_UidValidity_Uid",
                table: "MailMessages",
                columns: new[] { "AccountKey", "Folder", "UidValidity", "Uid" },
                unique: true);
        }
    }
}
