using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNonMoiCatalogueChangeLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AddedById",
                table: "MaintenanceCatalogueItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddedByName",
                table: "MaintenanceCatalogueItems",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PriceChangedAt",
                table: "MaintenanceCatalogueItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PriceChangedById",
                table: "MaintenanceCatalogueItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceChangedByName",
                table: "MaintenanceCatalogueItems",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MaintenanceCatalogueChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OldPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    NewPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ById = table.Column<Guid>(type: "uuid", nullable: false),
                    ByName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceCatalogueChanges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceCatalogueChanges_ItemId_CreatedAt",
                table: "MaintenanceCatalogueChanges",
                columns: new[] { "ItemId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceCatalogueChanges");

            migrationBuilder.DropColumn(
                name: "AddedById",
                table: "MaintenanceCatalogueItems");

            migrationBuilder.DropColumn(
                name: "AddedByName",
                table: "MaintenanceCatalogueItems");

            migrationBuilder.DropColumn(
                name: "PriceChangedAt",
                table: "MaintenanceCatalogueItems");

            migrationBuilder.DropColumn(
                name: "PriceChangedById",
                table: "MaintenanceCatalogueItems");

            migrationBuilder.DropColumn(
                name: "PriceChangedByName",
                table: "MaintenanceCatalogueItems");
        }
    }
}
