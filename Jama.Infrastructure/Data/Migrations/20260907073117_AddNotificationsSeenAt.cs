using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationsSeenAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NotificationsSeenAt",
                table: "AdminUsers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotificationsSeenAt",
                table: "AdminUsers");
        }
    }
}
