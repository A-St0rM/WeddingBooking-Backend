using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Readiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "readiness_items",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    responsible = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_readiness_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_readiness_items_bookings_booking_id",
                        column: x => x.booking_id,
                        principalSchema: "app",
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_readiness_items_booking_id",
                schema: "app",
                table: "readiness_items",
                column: "booking_id");

            // Bookings made before this migration get the standard items, as new bookings do.
            migrationBuilder.Sql("""
                INSERT INTO app.readiness_items (id, booking_id, category, title, status, updated_at)
                SELECT gen_random_uuid(), b.id, d.category, d.title, 'NotStarted', now()
                FROM app.bookings b
                CROSS JOIN (VALUES
                    ('Contact', 'Kontakt'),
                    ('TableSetting', 'Opdækning og opstilling'),
                    ('Decoration', 'Pynt og blomster'),
                    ('Food', 'Mad: menu, kuverter og allergier'),
                    ('PostEventInvoicing', 'Efterfakturering')) AS d(category, title)
                WHERE NOT EXISTS (SELECT 1 FROM app.readiness_items r WHERE r.booking_id = b.id AND r.category = d.category);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "readiness_items",
                schema: "app");
        }
    }
}
