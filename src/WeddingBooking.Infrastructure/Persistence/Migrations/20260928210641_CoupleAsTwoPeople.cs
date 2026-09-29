using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CoupleAsTwoPeople : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Written by hand: add, move the data, tighten, drop the old. A generated
            // migration would have dropped the names before reading them.
            migrationBuilder.Sql("""
                ALTER TABLE app.contact_persons RENAME COLUMN is_primary TO is_payer;
                ALTER TABLE app.contact_persons
                    ADD first_name character varying(100),
                    ADD last_name character varying(100),
                    ADD position integer;

                -- Every person already there: the first word is the first name, the rest the last name.
                UPDATE app.contact_persons SET
                    first_name = split_part(btrim(name), ' ', 1),
                    last_name = nullif(btrim(substr(btrim(name), length(split_part(btrim(name), ' ', 1)) + 1)), '');

                -- A couple written as one name, with fewer than two people: split the name on og, & or +,
                -- and add whoever is missing.
                WITH couple AS (
                    SELECT c.id, regexp_split_to_array(btrim(c.name), '\s+(?:og|&|\+)\s+|\s*[&+]\s*', 'i') AS parts
                    FROM app.customers c
                    WHERE (SELECT count(*) FROM app.contact_persons p WHERE p.customer_id = c.id) < 2
                ), two AS (
                    SELECT id, btrim(parts[1]) AS a, btrim(parts[2]) AS b FROM couple
                    WHERE array_length(parts, 1) = 2 AND btrim(parts[1]) <> '' AND btrim(parts[2]) <> ''
                )
                INSERT INTO app.contact_persons (id, customer_id, name, first_name, is_payer)
                SELECT gen_random_uuid(), two.id, n.first_name, n.first_name, false
                FROM two CROSS JOIN LATERAL (VALUES (two.a), (two.b)) AS n(first_name)
                WHERE NOT EXISTS (
                    SELECT 1 FROM app.contact_persons p WHERE p.customer_id = two.id AND lower(p.first_name) = lower(n.first_name));

                -- The order they were named in: whoever comes first in the old name is number 1.
                UPDATE app.contact_persons p SET position = r.position
                FROM (
                    SELECT p.id, row_number() OVER (
                        PARTITION BY p.customer_id
                        ORDER BY nullif(strpos(lower(c.name), lower(p.first_name)), 0) NULLS LAST, p.id) AS position
                    FROM app.contact_persons p JOIN app.customers c ON c.id = p.customer_id
                ) r
                WHERE p.id = r.id;

                -- Exactly one payer: the old primary contact, or else the one named first.
                UPDATE app.contact_persons p SET is_payer = true
                WHERE p.position = 1
                  AND NOT EXISTS (SELECT 1 FROM app.contact_persons q WHERE q.customer_id = p.customer_id AND q.is_payer);

                ALTER TABLE app.contact_persons
                    ALTER COLUMN first_name SET NOT NULL,
                    ALTER COLUMN position SET NOT NULL,
                    DROP COLUMN name;
                ALTER TABLE app.customers DROP COLUMN name;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "first_name",
                schema: "app",
                table: "contact_persons");

            migrationBuilder.DropColumn(
                name: "last_name",
                schema: "app",
                table: "contact_persons");

            migrationBuilder.DropColumn(
                name: "position",
                schema: "app",
                table: "contact_persons");

            migrationBuilder.RenameColumn(
                name: "is_payer",
                schema: "app",
                table: "contact_persons",
                newName: "is_primary");

            migrationBuilder.AddColumn<string>(
                name: "name",
                schema: "app",
                table: "customers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "name",
                schema: "app",
                table: "contact_persons",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
    }
}
