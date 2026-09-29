using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace WeddingBooking.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    /// <summary>
    /// Registers the database. The API asks for persistence and is told nothing
    /// about which engine backs it.
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, string? connectionString) =>
        services.AddDbContext<AppDbContext>(options => Configure(options, connectionString));

    internal static void Configure(DbContextOptionsBuilder options, string? connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema))
            .UseSnakeCaseNamingConvention();
}
