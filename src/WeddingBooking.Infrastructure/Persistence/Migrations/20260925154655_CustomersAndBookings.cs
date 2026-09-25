using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomersAndBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customers",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_date = table.Column<DateOnly>(type: "date", nullable: false),
                    guest_count = table.Column<int>(type: "integer", nullable: false),
                    payment_terms = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookings", x => x.id);
                    table.CheckConstraint("ck_bookings_guest_count", "guest_count BETWEEN 1 AND 350");
                    table.ForeignKey(
                        name: "fk_bookings_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "app",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contact_persons",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contact_persons", x => x.id);
                    table.ForeignKey(
                        name: "fk_contact_persons_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "app",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_customer_id",
                schema: "app",
                table: "bookings",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_event_date",
                schema: "app",
                table: "bookings",
                column: "event_date");

            migrationBuilder.CreateIndex(
                name: "ix_contact_persons_customer_id",
                schema: "app",
                table: "contact_persons",
                column: "customer_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bookings",
                schema: "app");

            migrationBuilder.DropTable(
                name: "contact_persons",
                schema: "app");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "app");
        }
    }
}
