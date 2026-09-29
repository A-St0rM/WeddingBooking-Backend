using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Communication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "viewing_at",
                schema: "app",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "viewing_host_id",
                schema: "app",
                table: "bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "message_templates",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    cc_viewing_host = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_message_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notes",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: true),
                    author_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recipients = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_notes_bookings_booking_id",
                        column: x => x.booking_id,
                        principalSchema: "app",
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_viewing_host_id",
                schema: "app",
                table: "bookings",
                column: "viewing_host_id");

            migrationBuilder.CreateIndex(
                name: "ix_notes_booking_id_created_at",
                schema: "app",
                table: "notes",
                columns: new[] { "booking_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_bookings_users_viewing_host_id",
                schema: "app",
                table: "bookings",
                column: "viewing_host_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Johan's standard texts, as a start. He edits them under Tekstbidder.
            migrationBuilder.Sql("""
                INSERT INTO app.message_templates (id, name, subject, body, cc_viewing_host, sort_order) VALUES
                ('01990000-0000-7000-8000-000000000201', 'Svar på forespørgsel', 'Bryllup på Engestofte Gods {{dato}}',
                 E'Kære {{parret}}\n\nTak for jeres henvendelse, og tillykke med forlovelsen. Vi vil meget gerne være rammen om jeres bryllup {{dato}}.\n\nJeg har vedhæftet vores grundpakke med priser, minimumspakke og tilkøbsmuligheder, så I kan se, hvad et bryllup for {{gæster}} gæster indeholder hos os.\n\nDen bedste måde at mærke stedet på er at se det. Vi viser gerne rundt på godset — skriv, hvornår det passer jer.\n\nMange hilsner\n{{afsender}}\nEngestofte Gods', false, 0),
                ('01990000-0000-7000-8000-000000000202', 'Mødebekræftelse — fremvisning', 'Fremvisning på Engestofte Gods {{fremvisning}}',
                 E'Kære {{parret}}\n\nTak for en god snak. Hermed bekræftelse af jeres fremvisning på Engestofte Gods {{fremvisning}}.\n\nDet er {{vært}}, der tager imod jer og viser rundt. Mangler I noget på dagen, kan I ringe direkte til {{vært}} på {{vært_telefon}}.\n\nAdressen er Engestoftevej, 4930 Maribo.\n\nVi glæder os til at se jer.\n\nMange hilsner\n{{afsender}}\nEngestofte Gods', true, 1),
                ('01990000-0000-7000-8000-000000000203', 'Tilbud', 'Tilbud på jeres bryllup {{dato}}',
                 E'Kære {{parret}}\n\nTak for en dejlig fremvisning. Som aftalt sender jeg her tilbud på jeres bryllup {{dato}} for {{gæster}} gæster. Den samlede pris er {{pris}}.\n\nAftalebekræftelsen og vores generelle betingelser er vedhæftet. Læs dem i ro og mag, og send den underskrevne aftale retur, når I er klar. Datoen er sikret, når reservationsgebyret er betalt.\n\nMange hilsner\n{{afsender}}\nEngestofte Gods', false, 2),
                ('01990000-0000-7000-8000-000000000204', 'Opfølgning', 'Hvordan går det med planerne?',
                 E'Kære {{fornavn}}\n\nJeg ville bare høre, hvordan det går med jeres planer for {{dato}}. Har I spørgsmål til tilbuddet eller til dagen, så skriv eller ring endelig.\n\nMange hilsner\n{{afsender}}\nEngestofte Gods', false, 3);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_bookings_users_viewing_host_id",
                schema: "app",
                table: "bookings");

            migrationBuilder.DropTable(
                name: "message_templates",
                schema: "app");

            migrationBuilder.DropTable(
                name: "notes",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "ix_bookings_viewing_host_id",
                schema: "app",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "viewing_at",
                schema: "app",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "viewing_host_id",
                schema: "app",
                table: "bookings");
        }
    }
}
