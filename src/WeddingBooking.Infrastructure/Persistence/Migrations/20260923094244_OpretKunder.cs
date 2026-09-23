using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingBooking.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Replaces the free-text kundenavn on a Booking with a real Kunde.
    /// </summary>
    /// <remarks>
    /// Hand-written. The scaffolded version dropped kundenavn and defaulted every
    /// booking to a zero GUID, which orphans every existing row and then fails on
    /// the foreign key. The order below — create, add nullable, backfill, tighten,
    /// drop — is what makes it safe to run against a database with data in it.
    ///
    /// <para>One Kunde is created per existing Booking, not per distinct name.
    /// Two bookings with the same name typed in are probably the same couple, but
    /// "probably" is not good enough to merge two customers permanently. Merging
    /// later is easy; unpicking a wrong merge is not.</para>
    /// </remarks>
    public partial class OpretKunder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                constraints: table => table.PrimaryKey("PK_kunder", x => x.id));

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
                name: "IX_kontaktpersoner_kunde_id",
                schema: "app", table: "kontaktpersoner", column: "kunde_id");

            migrationBuilder.CreateIndex(
                name: "IX_kunder_navn",
                schema: "app", table: "kunder", column: "navn");

            // Nullable to begin with: the rows that need a value do not have one yet.
            migrationBuilder.AddColumn<Guid>(
                name: "kunde_id", schema: "app", table: "bookinger",
                type: "uuid", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "betaler_kunde_id", schema: "app", table: "bookinger",
                type: "uuid", nullable: true);

            // Give every existing Booking a Kunde carrying the name it had, and
            // make that customer the payer — which is the default anyway.
            migrationBuilder.Sql("""
                WITH ny AS (
                    SELECT b.id AS booking_id,
                           gen_random_uuid() AS kunde_id,
                           COALESCE(NULLIF(btrim(b.kundenavn), ''), 'Uden navn') AS navn
                    FROM app.bookinger b
                ), indsat AS (
                    INSERT INTO app.kunder (id, navn, oprettet)
                    SELECT kunde_id, navn, now() FROM ny
                    RETURNING id
                )
                UPDATE app.bookinger b
                SET kunde_id = ny.kunde_id,
                    betaler_kunde_id = ny.kunde_id
                FROM ny
                WHERE ny.booking_id = b.id;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "kunde_id", schema: "app", table: "bookinger",
                type: "uuid", nullable: false, oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "betaler_kunde_id", schema: "app", table: "bookinger",
                type: "uuid", nullable: false, oldNullable: true);

            migrationBuilder.DropColumn(
                name: "kundenavn", schema: "app", table: "bookinger");

            migrationBuilder.CreateIndex(
                name: "IX_bookinger_kunde_id",
                schema: "app", table: "bookinger", column: "kunde_id");

            migrationBuilder.CreateIndex(
                name: "IX_bookinger_betaler_kunde_id",
                schema: "app", table: "bookinger", column: "betaler_kunde_id");

            // Restrict, not Cascade: deleting a customer must never quietly take
            // their weddings with it.
            migrationBuilder.AddForeignKey(
                name: "FK_bookinger_kunder_kunde_id",
                schema: "app", table: "bookinger", column: "kunde_id",
                principalSchema: "app", principalTable: "kunder", principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_bookinger_kunder_betaler_kunde_id",
                schema: "app", table: "bookinger", column: "betaler_kunde_id",
                principalSchema: "app", principalTable: "kunder", principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Reversible in shape, not in substance: addresses and contact people have
        /// nowhere to go in a single text column and are lost on the way back.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kundenavn", schema: "app", table: "bookinger",
                type: "character varying(200)", maxLength: 200, nullable: true);

            migrationBuilder.Sql("""
                UPDATE app.bookinger b
                SET kundenavn = k.navn
                FROM app.kunder k
                WHERE k.id = b.kunde_id;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_bookinger_kunder_kunde_id", schema: "app", table: "bookinger");
            migrationBuilder.DropForeignKey(
                name: "FK_bookinger_kunder_betaler_kunde_id", schema: "app", table: "bookinger");
            migrationBuilder.DropIndex(
                name: "IX_bookinger_kunde_id", schema: "app", table: "bookinger");
            migrationBuilder.DropIndex(
                name: "IX_bookinger_betaler_kunde_id", schema: "app", table: "bookinger");
            migrationBuilder.DropColumn(name: "kunde_id", schema: "app", table: "bookinger");
            migrationBuilder.DropColumn(name: "betaler_kunde_id", schema: "app", table: "bookinger");
            migrationBuilder.DropTable(name: "kontaktpersoner", schema: "app");
            migrationBuilder.DropTable(name: "kunder", schema: "app");
        }
    }
}
