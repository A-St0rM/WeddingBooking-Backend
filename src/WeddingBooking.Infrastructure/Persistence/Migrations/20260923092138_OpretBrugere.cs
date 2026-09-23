using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OpretBrugere : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "brugere",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    navn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    rolle = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    auth_bruger_id = table.Column<Guid>(type: "uuid", nullable: true),
                    oprettet = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sidste_login = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brugere", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_brugere_auth_bruger_id",
                schema: "app",
                table: "brugere",
                column: "auth_bruger_id");

            migrationBuilder.CreateIndex(
                name: "IX_brugere_email",
                schema: "app",
                table: "brugere",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "brugere",
                schema: "app");
        }
    }
}
