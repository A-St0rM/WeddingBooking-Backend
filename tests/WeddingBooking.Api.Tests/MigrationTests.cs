using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Tests;

/// <summary>
/// The migrations that move data, run on data shaped like before them — in a
/// database of their own, so the shared one is untouched.
/// </summary>
public sealed class MigrationTests
{
    [IntegrationFact]
    public async Task Couples_written_as_one_name_become_two_people_with_one_payer()
    {
        var connection = await FreshDatabaseAsync("migration_check");
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection, n => n.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema))
            .UseSnakeCaseNamingConvention().Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260925180830_TrelloOrigin");

        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO app.customers (id, name, created_at) VALUES
              ('00000000-0000-0000-0000-00000000000a', 'Tobias og Cecilie', now()),
              ('00000000-0000-0000-0000-00000000000b', 'Anna & Mikkel', now()),
              ('00000000-0000-0000-0000-00000000000c', 'Sofie', now()),
              ('00000000-0000-0000-0000-00000000000d', 'Ida og Frederik', now());
            INSERT INTO app.contact_persons (id, customer_id, name, email, phone, is_primary) VALUES
              (gen_random_uuid(), '00000000-0000-0000-0000-00000000000b', 'Mikkel Hansen', 'mikkel@example.dk', NULL, true),
              (gen_random_uuid(), '00000000-0000-0000-0000-00000000000d', 'Frederik', NULL, NULL, false),
              (gen_random_uuid(), '00000000-0000-0000-0000-00000000000d', 'Ida Berg', 'ida@example.dk', NULL, true);
            """);

        await migrator.MigrateAsync();

        var customers = await db.Customers.AsNoTracking().ToDictionaryAsync(c => c.Id.ToString()[^1..]);
        Assert.Equal("Tobias og Cecilie", customers["a"].Name);
        Assert.Equal("Tobias", customers["a"].Payer!.FirstName);

        Assert.Equal("Anna og Mikkel", customers["b"].Name);
        Assert.Equal("Mikkel Hansen", customers["b"].Payer!.FullName); // the old primary contact stays the payer
        Assert.Equal("mikkel@example.dk", customers["b"].Payer!.Email);

        Assert.Empty(customers["c"].Contacts); // cannot be split: left for the report

        Assert.Equal("Ida og Frederik", customers["d"].Name);
        Assert.Equal("Berg", customers["d"].Payer!.LastName);
        Assert.All(customers.Values, c => Assert.True(c.Contacts.Count(p => p.IsPayer) <= 1));
    }

    private static async Task<string> FreshDatabaseAsync(string name)
    {
        await using (var admin = new NpgsqlConnection(ApiFactory.ConnectionString))
        {
            await admin.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {name} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", admin);
            await create.ExecuteNonQueryAsync();
        }
        return new NpgsqlConnectionStringBuilder(ApiFactory.ConnectionString) { Database = name }.ConnectionString;
    }
}
