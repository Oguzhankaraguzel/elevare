using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReminderEmailRetries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmailAttempts",
                table: "UserReminders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "EmailFailed",
                table: "UserReminders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextEmailAttemptAt",
                table: "UserReminders",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailAttempts",
                table: "UserReminders");

            migrationBuilder.DropColumn(
                name: "EmailFailed",
                table: "UserReminders");

            migrationBuilder.DropColumn(
                name: "NextEmailAttemptAt",
                table: "UserReminders");
        }
    }
}
