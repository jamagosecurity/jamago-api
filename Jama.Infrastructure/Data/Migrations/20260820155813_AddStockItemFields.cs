using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStockItemFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Cameras",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Cctv");

            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                table: "Cameras",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HsnCode",
                table: "Cameras",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemName",
                table: "Cameras",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ItemType",
                table: "Cameras",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Product");

            migrationBuilder.AddColumn<int>(
                name: "LowStock",
                table: "Cameras",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Margin",
                table: "Cameras",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Cameras",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Rate",
                table: "Cameras",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SearchKey",
                table: "Cameras",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SupplierCost",
                table: "Cameras",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                table: "Cameras",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Uom",
                table: "Cameras",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Piece");

            migrationBuilder.AddColumn<string>(
                name: "WarrantyUnit",
                table: "Cameras",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarrantyValue",
                table: "Cameras",
                type: "integer",
                nullable: true);

            // Rows created before ItemName existed have it defaulted to ''. A
            // required field left blank would fail validation the first time
            // someone edited the line, so give each a name built from what it
            // already had: brand, model number and type.
            migrationBuilder.Sql(
                """
                UPDATE "Cameras"
                SET "ItemName" = TRIM(BOTH ' ' FROM CONCAT_WS(' ', "Brand", NULLIF("ModelNo", ''), "Type"))
                WHERE "ItemName" = '';
                """);

            migrationBuilder.CreateTable(
                name: "CameraImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CameraId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CameraImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CameraImages_Cameras_CameraId",
                        column: x => x.CameraId,
                        principalTable: "Cameras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CameraImages_CameraId_SortOrder",
                table: "CameraImages",
                columns: new[] { "CameraId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CameraImages");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "Discount",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "HsnCode",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "ItemName",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "ItemType",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "LowStock",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "Margin",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "Rate",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "SearchKey",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "SupplierCost",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "Uom",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "WarrantyUnit",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "WarrantyValue",
                table: "Cameras");
        }
    }
}
