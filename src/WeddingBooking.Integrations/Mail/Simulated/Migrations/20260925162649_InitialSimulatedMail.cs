using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingBooking.Integrations.Mail.Simulated.Migrations
{
    /// <inheritdoc />
    public partial class InitialSimulatedMail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "mail_sim");

            migrationBuilder.CreateTable(
                name: "outbox",
                schema: "mail_sim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    reply_to = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    to = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    cc = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_sent_at",
                schema: "mail_sim",
                table: "outbox",
                column: "sent_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox",
                schema: "mail_sim");
        }
    }
}
