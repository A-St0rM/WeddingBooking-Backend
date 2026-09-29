using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BookingOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "origin",
                schema: "app",
                table: "bookings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Staff");

            // Cases imported from Trello say so.
            migrationBuilder.Sql("UPDATE app.bookings SET origin = 'Trello' WHERE trello_card_id IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "origin",
                schema: "app",
                table: "bookings");
        }
    }
}
