using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrelloMirror : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "trello_check_item_id",
                schema: "app",
                table: "readiness_items",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "trello_synced_done",
                schema: "app",
                table: "readiness_items",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "trello_check_item_id",
                schema: "app",
                table: "readiness_items");

            migrationBuilder.DropColumn(
                name: "trello_synced_done",
                schema: "app",
                table: "readiness_items");
        }
    }
}
