using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TourFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProtectBookingRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingAssignments_Bookings_BookingId",
                table: "BookingAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_BookingStatusHistories_Bookings_BookingId",
                table: "BookingStatusHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Bookings_BookingId",
                table: "Payments");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingAssignments_Bookings_BookingId",
                table: "BookingAssignments",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BookingStatusHistories_Bookings_BookingId",
                table: "BookingStatusHistories",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Bookings_BookingId",
                table: "Payments",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingAssignments_Bookings_BookingId",
                table: "BookingAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_BookingStatusHistories_Bookings_BookingId",
                table: "BookingStatusHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Bookings_BookingId",
                table: "Payments");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingAssignments_Bookings_BookingId",
                table: "BookingAssignments",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BookingStatusHistories_Bookings_BookingId",
                table: "BookingStatusHistories",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Bookings_BookingId",
                table: "Payments",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
