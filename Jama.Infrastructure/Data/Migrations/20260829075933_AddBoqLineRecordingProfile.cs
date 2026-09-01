using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBoqLineRecordingProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BitrateMbps",
                table: "BoqLines",
                type: "numeric(8,3)",
                precision: 8,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Resolution",
                table: "BoqLines",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                // NOT "" as the scaffolder emits, for the reason AddCameraRecordingProfile
                // records: the column stores the enum NAME, and an empty string cannot be
                // materialised back into CameraResolution, so every existing line would
                // fail to read.
                defaultValue: "Unspecified");

            // Backfill from the catalogue for lines whose item still exists.
            //
            // These columns exist so a bill stops depending on the live catalogue, but
            // rows written before today never captured a profile, and the catalogue is
            // the only place their figures have ever been. Copying now is strictly
            // better than leaving them blank: it is the same value the old live-read
            // would have produced, frozen from this point on.
            //
            // Lines whose CameraId is already null keep Unspecified and null. Their item
            // is gone and there is nothing left to copy — which is precisely the failure
            // this change stops happening again.
            migrationBuilder.Sql("""
                UPDATE "BoqLines" AS l
                SET "Resolution" = c."Resolution",
                    "BitrateMbps" = c."BitrateMbps"
                FROM "Cameras" AS c
                WHERE l."CameraId" = c."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BitrateMbps",
                table: "BoqLines");

            migrationBuilder.DropColumn(
                name: "Resolution",
                table: "BoqLines");
        }
    }
}
