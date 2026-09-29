using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrelloOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "trello_card_id",
                schema: "app",
                table: "bookings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_bookings_trello_card_id",
                schema: "app",
                table: "bookings",
                column: "trello_card_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_bookings_trello_card_id",
                schema: "app",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "trello_card_id",
                schema: "app",
                table: "bookings");
        }
    }
}
