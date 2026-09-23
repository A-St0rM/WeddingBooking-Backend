using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OpretKunder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "kundenavn",
                schema: "app",
                table: "bookinger");

            migrationBuilder.AddColumn<Guid>(
                name: "betaler_kunde_id",
                schema: "app",
                table: "bookinger",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "kunde_id",
                schema: "app",
                table: "bookinger",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "kunder",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    navn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    adresse = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    postnr = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    by = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    economic_customer_number = table.Column<int>(type: "integer", nullable: true),
                    oprettet = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kunder", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kontaktpersoner",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kunde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    navn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    telefon = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    er_primaer = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kontaktpersoner", x => x.id);
                    table.ForeignKey(
                        name: "FK_kontaktpersoner_kunder_kunde_id",
                        column: x => x.kunde_id,
                        principalSchema: "app",
                        principalTable: "kunder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bookinger_betaler_kunde_id",
                schema: "app",
                table: "bookinger",
                column: "betaler_kunde_id");

            migrationBuilder.CreateIndex(
                name: "IX_bookinger_kunde_id",
                schema: "app",
                table: "bookinger",
                column: "kunde_id");

            migrationBuilder.CreateIndex(
                name: "IX_kontaktpersoner_kunde_id",
                schema: "app",
                table: "kontaktpersoner",
                column: "kunde_id");

            migrationBuilder.CreateIndex(
                name: "IX_kunder_navn",
                schema: "app",
                table: "kunder",
                column: "navn");

            migrationBuilder.AddForeignKey(
                name: "FK_bookinger_kunder_betaler_kunde_id",
                schema: "app",
                table: "bookinger",
                column: "betaler_kunde_id",
                principalSchema: "app",
                principalTable: "kunder",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_bookinger_kunder_kunde_id",
                schema: "app",
                table: "bookinger",
                column: "kunde_id",
                principalSchema: "app",
                principalTable: "kunder",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bookinger_kunder_betaler_kunde_id",
                schema: "app",
                table: "bookinger");

            migrationBuilder.DropForeignKey(
                name: "FK_bookinger_kunder_kunde_id",
                schema: "app",
                table: "bookinger");

            migrationBuilder.DropTable(
                name: "kontaktpersoner",
                schema: "app");

            migrationBuilder.DropTable(
                name: "kunder",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "IX_bookinger_betaler_kunde_id",
                schema: "app",
                table: "bookinger");

            migrationBuilder.DropIndex(
                name: "IX_bookinger_kunde_id",
                schema: "app",
                table: "bookinger");

            migrationBuilder.DropColumn(
                name: "betaler_kunde_id",
                schema: "app",
                table: "bookinger");

            migrationBuilder.DropColumn(
                name: "kunde_id",
                schema: "app",
                table: "bookinger");

            migrationBuilder.AddColumn<string>(
                name: "kundenavn",
                schema: "app",
                table: "bookinger",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
