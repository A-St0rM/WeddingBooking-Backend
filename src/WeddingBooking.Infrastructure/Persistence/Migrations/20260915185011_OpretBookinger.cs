using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OpretBookinger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.CreateTable(
                name: "bookinger",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kundenavn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    bryllupsdato = table.Column<DateOnly>(type: "date", nullable: true),
                    gaesteantal = table.Column<int>(type: "integer", nullable: true),
                    oprettet = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookinger", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bookinger_bryllupsdato",
                schema: "app",
                table: "bookinger",
                column: "bryllupsdato");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bookinger",
                schema: "app");
        }
    }
}
