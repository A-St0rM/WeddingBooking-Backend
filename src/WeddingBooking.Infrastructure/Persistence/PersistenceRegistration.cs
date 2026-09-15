using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WeddingBooking.Infrastructure.Persistence;

public static class PersistenceRegistration
{
    /// <summary>Everything of ours lives here, never in public. See ADR-0007.</summary>
    public const string Schema = "app";

    /// <summary>
    /// Registers the database. The API asks for persistence and is told nothing
    /// about which engine backs it; swapping Postgres out would not touch Program.cs.
    /// </summary>
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        string? connectionString)
    {
        return services.AddDbContext<WeddingBookingDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", Schema)));
    }
}
