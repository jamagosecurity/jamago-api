using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBoqLineType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "BoqLines",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            // Backfill from the catalogue where the item still exists, for the
            // same reason AddBoqLineRecordingProfile did: these columns exist so
            // a bill stops depending on live stock, and copying now is the value
            // the old live read would have produced, frozen from here on.
            migrationBuilder.Sql("""
                UPDATE "BoqLines" AS l
                SET "Type" = NULLIF(c."Type", '')
                FROM "Cameras" AS c
                WHERE l."CameraId" = c."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "BoqLines");
        }
    }
}
