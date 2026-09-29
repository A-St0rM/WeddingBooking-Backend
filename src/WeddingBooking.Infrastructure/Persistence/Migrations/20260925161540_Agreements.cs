using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Agreements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "agreement_clarification",
                schema: "app",
                table: "bookings",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "stored_files",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stored_files", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agreement_versions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    generated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    clarification = table.Column<string>(type: "text", nullable: true),
                    pdf_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    signed_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    signed_uploaded_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agreement_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_agreement_versions_bookings_booking_id",
                        column: x => x.booking_id,
                        principalSchema: "app",
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_agreement_versions_file_record_pdf_file_id",
                        column: x => x.pdf_file_id,
                        principalSchema: "app",
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agreement_versions_file_record_signed_file_id",
                        column: x => x.signed_file_id,
                        principalSchema: "app",
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agreement_versions_booking_id_number",
                schema: "app",
                table: "agreement_versions",
                columns: new[] { "booking_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agreement_versions_pdf_file_id",
                schema: "app",
                table: "agreement_versions",
                column: "pdf_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_agreement_versions_signed_file_id",
                schema: "app",
                table: "agreement_versions",
                column: "signed_file_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agreement_versions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "stored_files",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "agreement_clarification",
                schema: "app",
                table: "bookings");
        }
    }
}
