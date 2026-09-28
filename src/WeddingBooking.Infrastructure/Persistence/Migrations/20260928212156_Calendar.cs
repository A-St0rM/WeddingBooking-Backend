using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Calendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "calendar_blocks",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    host_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_blocks", x => x.id);
                    table.ForeignKey(
                        name: "fk_calendar_blocks_users_host_id",
                        column: x => x.host_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "host_availability",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    host_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    from = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    to = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_host_availability", x => x.id);
                    table.ForeignKey(
                        name: "fk_host_availability_users_host_id",
                        column: x => x.host_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_blocks_host_id",
                schema: "app",
                table: "calendar_blocks",
                column: "host_id");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_blocks_start",
                schema: "app",
                table: "calendar_blocks",
                column: "start");

            migrationBuilder.CreateIndex(
                name: "ix_host_availability_host_id",
                schema: "app",
                table: "host_availability",
                column: "host_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calendar_blocks",
                schema: "app");

            migrationBuilder.DropTable(
                name: "host_availability",
                schema: "app");
        }
    }
}
