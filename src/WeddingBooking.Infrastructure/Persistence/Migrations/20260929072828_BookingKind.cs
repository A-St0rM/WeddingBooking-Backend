using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BookingKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kind",
                schema: "app",
                table: "bookings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Wedding");

            // Viewings booked from the website that nobody has confirmed as a wedding yet.
            migrationBuilder.Sql("UPDATE app.bookings SET kind = 'Viewing' WHERE origin = 'Website' AND viewing_at IS NOT NULL AND stage IN ('Requested', 'Viewing');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "kind",
                schema: "app",
                table: "bookings");
        }
    }
}
