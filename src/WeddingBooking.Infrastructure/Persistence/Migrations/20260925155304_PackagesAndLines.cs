using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PackagesAndLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "surcharge_amount",
                schema: "app",
                table: "bookings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "surcharge_threshold",
                schema: "app",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "booking_lines",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    product_group = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    pricing = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    minimum_quantity = table.Column<int>(type: "integer", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    source_package_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_package_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_booking_lines_bookings_booking_id",
                        column: x => x.booking_id,
                        principalSchema: "app",
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "packages",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_packages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pricing_settings",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    small_wedding_threshold = table.Column<int>(type: "integer", nullable: false),
                    small_wedding_surcharge = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pricing_settings", x => x.id);
                    table.CheckConstraint("ck_pricing_settings_singleton", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "package_lines",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    product_group = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    pricing = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    minimum_quantity = table.Column<int>(type: "integer", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_package_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_package_lines_packages_package_id",
                        column: x => x.package_id,
                        principalSchema: "app",
                        principalTable: "packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_booking_id",
                schema: "app",
                table: "booking_lines",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_package_lines_package_id",
                schema: "app",
                table: "package_lines",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "ix_packages_year",
                schema: "app",
                table: "packages",
                column: "year");

            // Starting data, written by hand. The 2026 prices are the ones in the
            // requirements; the Friday event's price is a placeholder until the
            // estate gives one. The surcharge amount is a placeholder too — an
            // owner sets the real one under Pakker.
            migrationBuilder.Sql("""
                INSERT INTO app.pricing_settings (id, small_wedding_threshold, small_wedding_surcharge, updated_at)
                VALUES (1, 60, 7500, now());

                INSERT INTO app.packages (id, name, kind, year, description, is_active) VALUES
                  ('01990000-0000-7000-8000-000000000001', 'Grundpakke 2026', 'Base', 2026,
                   'Treretters menu med vand, vin og øl ad libitum.', true),
                  ('01990000-0000-7000-8000-000000000002', 'Natmad 2026', 'AddOn', 2026, NULL, true),
                  ('01990000-0000-7000-8000-000000000003', 'Spiritus og cocktailbar 2026', 'AddOn', 2026, NULL, true),
                  ('01990000-0000-7000-8000-000000000004', 'Fredagsarrangement 2026', 'AddOn', 2026,
                   'Let grill aftenen før for de nærmeste. Minimum 30 gæster.', true);

                INSERT INTO app.package_lines (id, package_id, description, product_group, pricing, unit_price, quantity, minimum_quantity, sort_order) VALUES
                  ('01990000-0000-7000-8000-000000000101', '01990000-0000-7000-8000-000000000001',
                   'Treretters menu med vand, vin og øl ad libitum', 'DinnerAndParty', 'PerGuest', 1495, 1, NULL, 0),
                  ('01990000-0000-7000-8000-000000000102', '01990000-0000-7000-8000-000000000002',
                   'Natmad', 'LateNight', 'PerGuest', 100, 1, NULL, 0),
                  ('01990000-0000-7000-8000-000000000103', '01990000-0000-7000-8000-000000000003',
                   'Spiritus og cocktailbar', 'Bar', 'PerGuest', 155, 1, NULL, 0),
                  ('01990000-0000-7000-8000-000000000104', '01990000-0000-7000-8000-000000000004',
                   'Fredagsarrangement, let grill', 'FridayEvent', 'Fixed', 250, 30, 30, 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_lines",
                schema: "app");

            migrationBuilder.DropTable(
                name: "package_lines",
                schema: "app");

            migrationBuilder.DropTable(
                name: "pricing_settings",
                schema: "app");

            migrationBuilder.DropTable(
                name: "packages",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "surcharge_amount",
                schema: "app",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "surcharge_threshold",
                schema: "app",
                table: "bookings");
        }
    }
}
