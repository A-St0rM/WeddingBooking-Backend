using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WeddingBooking.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations add` run without a live database or a running app.
/// Migrations are authored offline; a real connection string is only needed to apply them.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<WeddingBookingDbContext>
{
    public WeddingBookingDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Database=weddingbooking;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<WeddingBookingDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new WeddingBookingDbContext(options);
    }
}
