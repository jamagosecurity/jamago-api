using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProformaInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "ProformaNumbers");

            migrationBuilder.CreateTable(
                name: "ProformaInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BoqId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    IssuedById = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedByName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProjectName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PoNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    PoDate = table.Column<DateOnly>(type: "date", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProformaInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProformaInvoices_Boqs_BoqId",
                        column: x => x.BoqId,
                        principalTable: "Boqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProformaInvoices_BoqId",
                table: "ProformaInvoices",
                column: "BoqId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProformaInvoices_Number",
                table: "ProformaInvoices",
                column: "Number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProformaInvoices");

            migrationBuilder.DropSequence(
                name: "ProformaNumbers");
        }
    }
}
