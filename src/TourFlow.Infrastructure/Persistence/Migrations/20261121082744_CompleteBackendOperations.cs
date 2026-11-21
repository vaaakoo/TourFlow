using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TourFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteBackendOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Payments",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Bookings",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: Array.Empty<byte>());

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "BookingAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "BookingAssignments",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "BookingAssignments");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "BookingAssignments");
        }
    }
}
