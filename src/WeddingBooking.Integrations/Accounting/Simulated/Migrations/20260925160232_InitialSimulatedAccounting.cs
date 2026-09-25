using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WeddingBooking.Integrations.Accounting.Simulated.Migrations
{
    /// <inheritdoc />
    public partial class InitialSimulatedAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "accounting_sim");

            migrationBuilder.CreateSequence<int>(
                name: "customer_numbers",
                schema: "accounting_sim",
                startValue: 1001L);

            migrationBuilder.CreateSequence<int>(
                name: "invoice_numbers",
                schema: "accounting_sim",
                startValue: 20001L);

            migrationBuilder.CreateSequence<int>(
                name: "quote_numbers",
                schema: "accounting_sim",
                startValue: 5001L);

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "accounting_sim",
                columns: table => new
                {
                    number = table.Column<int>(type: "integer", nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.number);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                schema: "accounting_sim",
                columns: table => new
                {
                    number = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    customer_number = table.Column<int>(type: "integer", nullable: false),
                    heading = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    booked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documents", x => x.number);
                    table.ForeignKey(
                        name: "fk_documents_customers_customer_number",
                        column: x => x.customer_number,
                        principalSchema: "accounting_sim",
                        principalTable: "customers",
                        principalColumn: "number",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attachments",
                schema: "accounting_sim",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    document_number = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    attached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachments", x => x.id);
                    table.ForeignKey(
                        name: "fk_attachments_documents_document_number",
                        column: x => x.document_number,
                        principalSchema: "accounting_sim",
                        principalTable: "documents",
                        principalColumn: "number",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_lines",
                schema: "accounting_sim",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    document_number = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_lines_documents_document_number",
                        column: x => x.document_number,
                        principalSchema: "accounting_sim",
                        principalTable: "documents",
                        principalColumn: "number",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                schema: "accounting_sim",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    document_number = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    registered_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.ForeignKey(
                        name: "fk_payments_documents_document_number",
                        column: x => x.document_number,
                        principalSchema: "accounting_sim",
                        principalTable: "documents",
                        principalColumn: "number",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attachments_document_number",
                schema: "accounting_sim",
                table: "attachments",
                column: "document_number");

            migrationBuilder.CreateIndex(
                name: "ix_customers_reference",
                schema: "accounting_sim",
                table: "customers",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_lines_document_number",
                schema: "accounting_sim",
                table: "document_lines",
                column: "document_number");

            migrationBuilder.CreateIndex(
                name: "ix_documents_customer_number",
                schema: "accounting_sim",
                table: "documents",
                column: "customer_number");

            migrationBuilder.CreateIndex(
                name: "ix_documents_idempotency_key",
                schema: "accounting_sim",
                table: "documents",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_document_number",
                schema: "accounting_sim",
                table: "payments",
                column: "document_number");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attachments",
                schema: "accounting_sim");

            migrationBuilder.DropTable(
                name: "document_lines",
                schema: "accounting_sim");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "accounting_sim");

            migrationBuilder.DropTable(
                name: "documents",
                schema: "accounting_sim");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "accounting_sim");

            migrationBuilder.DropSequence(
                name: "customer_numbers",
                schema: "accounting_sim");

            migrationBuilder.DropSequence(
                name: "invoice_numbers",
                schema: "accounting_sim");

            migrationBuilder.DropSequence(
                name: "quote_numbers",
                schema: "accounting_sim");
        }
    }
}
