using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountingReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "accounting_number",
                schema: "app",
                table: "customers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "signed_at",
                schema: "app",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "accounting_references",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    instalment = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accounting_references", x => x.id);
                    table.ForeignKey(
                        name: "fk_accounting_references_bookings_booking_id",
                        column: x => x.booking_id,
                        principalSchema: "app",
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounting_references_booking_id",
                schema: "app",
                table: "accounting_references",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_accounting_references_booking_id_instalment",
                schema: "app",
                table: "accounting_references",
                columns: new[] { "booking_id", "instalment" },
                unique: true,
                filter: "type = 'Invoice'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounting_references",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "accounting_number",
                schema: "app",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "signed_at",
                schema: "app",
                table: "bookings");
        }
    }
}
