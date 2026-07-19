using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffLoginAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AdminUserId",
                table: "Staff",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Staff_AdminUserId",
                table: "Staff",
                column: "AdminUserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Staff_AdminUsers_AdminUserId",
                table: "Staff",
                column: "AdminUserId",
                principalTable: "AdminUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Staff_AdminUsers_AdminUserId",
                table: "Staff");

            migrationBuilder.DropIndex(
                name: "IX_Staff_AdminUserId",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "AdminUserId",
                table: "Staff");
        }
    }
}
