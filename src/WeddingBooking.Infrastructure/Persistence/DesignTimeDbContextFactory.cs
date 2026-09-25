using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WeddingBooking.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations add` run with no live database. Migrations are
/// authored offline; a real connection is only needed to apply them.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                               ?? "Host=localhost;Database=design_time;Username=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>();
        PersistenceExtensions.Configure(options, connectionString);
        return new AppDbContext(options.Options);
    }
}
